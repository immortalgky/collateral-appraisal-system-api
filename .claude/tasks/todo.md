# Workflow Engine Enhancement - Todo List

## Enhancements
1. `TaskActivity`: assignment related variables, should be as below:
   - `assignee`: the user assigned to the task
   - `assignee_group`: the group assigned to the task
   - `assignment_strategy`: the strategy used for assignment (e.g., round-robin, random)

   - the output variables will be update to workflow context.


2. `DecisionActivity`: the variables for condition should take from workflow context, not from the activity context.

3. Enhance assignment strategy:
   - `group`: assign tasks to a group of users (pool). could be part of manual assignment. frontend will pass the user or group name.
   - additional strategy for route back case, task should be assigned to the user who completed the task last time. but can be overridden if needed.
   - in case the user is not available, the task should be assigned to replacement user first. if not available, then assign to the group or supervisor (configable).
   - update `TaskActivity` definition/properties to support above enhancement for react flow to get activity definition to configure the task.



4. is it better if the `TaskActivity` can be configured outside of the workflow definition? This would allow for more flexibility in task management.
   e.g. workflow definition can be used to define the task or available strategy, but the `TaskActivity` can be configured in a separate configuration file or database for below properties:
   - `assignee`: the user assigned to the task
   - `assignee_group`: the group assigned to the task
   - `assignment_strategy`: the strategy used for assignment (e.g., round-robin, random)
   - replacement strategy:
     - `replacement_user`: the user to be used as replacement if the assignee is not available
     - `supervisor`: the user to be used as supervisor if the assignee is not available
   - fallback strategy if cannot find any user to assign.

# Questions Before Implementation:

1. Route Back Strategy: How should we
   track previous task completions? Do you
   have an existing audit/history system?
   answer: We can use the existing workflow activityexecution history 

2. User Availability: How do we
   determine if a user is "available"? Do
   you have a user status system?
   answer: yes later we will have. that system will be used to set leave or replacement user. etc.

3. Replacement/Supervisor Configuration:
   Should this be:
   - Per-user configuration in database?
   - Per-role/group configuration?
   - Part of the workflow definition?
    answer: per user configuration from the system in item 2
   
4. Group Assignment: Should this create
   multiple assignments or assign to the
   group as a whole?
   answer: assign to the group (pool) which user in the group can pick up the task. just no specific user assigned to the task.

5. External Configuration: What's your
   preferred storage mechanism and
   integration approach?
   answer: we can use database to store the configuration, and the workflow engine can read the configuration from the database when needed.
