using Shared.Identity;

namespace Appraisal.Application.Features.CommitteeVoting.SubmitVote;

public class SubmitVoteCommandHandler(
    AppraisalDbContext dbContext,
    ICommitteeRepository committeeRepository,
    ICurrentUserService currentUser
) : ICommandHandler<SubmitVoteCommand, SubmitVoteResult>
{
    public async Task<SubmitVoteResult> Handle(SubmitVoteCommand command, CancellationToken cancellationToken)
    {
        // 1. Load the review
        var review = await dbContext.AppraisalReviews
            .FirstOrDefaultAsync(r => r.Id == command.ReviewId && r.AppraisalId == command.AppraisalId,
                cancellationToken)
            ?? throw new NotFoundException("AppraisalReview", command.ReviewId);

        if (review.ReviewLevel != "Committee")
            throw new InvalidOperationException("Voting is only allowed on Committee-level reviews.");

        if (review.Status != ReviewStatus.Pending)
            throw new InvalidOperationException("Review is no longer pending.");

        if (!review.CommitteeId.HasValue)
            throw new InvalidOperationException("No committee assigned to this review.");

        // 2. Load committee with members
        var committee = await committeeRepository.GetByIdWithMembersAsync(review.CommitteeId.Value, cancellationToken)
            ?? throw new NotFoundException("Committee", review.CommitteeId.Value);

        // 3. Find current user as active member
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("User is not authenticated.");

        var member = committee.Members.FirstOrDefault(m => m.UserId == userId && m.IsActive)
            ?? throw new InvalidOperationException("Current user is not an active member of this committee.");

        // 4. Check no duplicate vote
        var existingVote = await committeeRepository.GetVoteByReviewAndMemberAsync(
            review.Id, member.Id, cancellationToken);
        if (existingVote != null)
            throw new InvalidOperationException("You have already voted on this review.");

        // 5. Create vote
        var vote = CommitteeVote.Create(
            review.Id,
            member.Id,
            member.MemberName,
            member.Role,
            command.Vote,
            command.Remark);

        await committeeRepository.AddVoteAsync(vote, cancellationToken);

        var isAutoApproved = false;

        // 6. If RouteBack → immediate return
        if (command.Vote == "RouteBack")
        {
            review.Return(userId, "Route Back by committee member", command.Remark);
        }
        else
        {
            // 7. Tally votes and check majority
            var allVotes = (await committeeRepository.GetVotesByReviewIdAsync(review.Id, cancellationToken)).ToList();
            // Include the vote we just created (not yet saved)
            allVotes.Add(vote);

            var approveCount = allVotes.Count(v => v.Vote == "Approve");
            var rejectCount = allVotes.Count(v => v.Vote == "Reject");
            var abstainCount = allVotes.Count(v => v.Vote == "Abstain");

            review.RecordVotes(approveCount, rejectCount, abstainCount);

            var activeMemberCount = committee.Members.Count(m => m.IsActive);
            if (committee.HasMajority(approveCount, activeMemberCount))
            {
                review.Approve(userId, "Auto-approved by committee majority");
                isAutoApproved = true;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubmitVoteResult(
            vote.Id,
            review.Status.Code,
            isAutoApproved);
    }
}
