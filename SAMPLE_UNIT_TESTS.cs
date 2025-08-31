// Sample unit tests for the notification system
// Add these to your test project

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using Notification.Notification.Dtos;
using Notification.Notification.Hubs;
using Notification.Notification.Services;
using Shared.Time;

namespace Notification.Tests.Services
{
    [TestFixture]
    public class NotificationServiceTests
    {
        private Mock<IHubContext<NotificationHub>> _mockHubContext;
        private Mock<ILogger<NotificationService>> _mockLogger;
        private Mock<IDateTimeProvider> _mockDateTimeProvider;
        private Mock<IHubCallerClients> _mockClients;
        private Mock<IClientProxy> _mockClientProxy;
        private NotificationService _notificationService;

        [SetUp]
        public void SetUp()
        {
            _mockHubContext = new Mock<IHubContext<NotificationHub>>();
            _mockLogger = new Mock<ILogger<NotificationService>>();
            _mockDateTimeProvider = new Mock<IDateTimeProvider>();
            _mockClients = new Mock<IHubCallerClients>();
            _mockClientProxy = new Mock<IClientProxy>();

            _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);
            _mockHubContext.Setup(x => x.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(x => x.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

            _notificationService = new NotificationService(
                _mockHubContext.Object,
                _mockLogger.Object,
                _mockDateTimeProvider.Object);
        }

        [Test]
        public async Task SendTaskAssignedNotificationAsync_ShouldSendNotificationToUser()
        {
            // Arrange
            var notification = new TaskAssignedNotificationDto(
                Guid.NewGuid(),
                "Admin",
                "testuser",
                "U",
                123,
                "Admin",
                DateTime.UtcNow
            );

            // Act
            await _notificationService.SendTaskAssignedNotificationAsync(notification);

            // Assert
            _mockClients.Verify(x => x.Group("User_testuser"), Times.Once);
            _mockClientProxy.Verify(
                x => x.SendAsync("ReceiveNotification", It.IsAny<NotificationDto>(), default),
                Times.Once);
        }

        [Test]
        public async Task SendTaskCompletedNotificationAsync_ShouldSendGroupNotification()
        {
            // Arrange
            var notification = new TaskCompletedNotificationDto(
                Guid.NewGuid(),
                "Admin",
                "completeduser",
                "P",
                123,
                "AwaitingAssignment",
                "Admin",
                DateTime.UtcNow
            );

            // Act
            await _notificationService.SendTaskCompletedNotificationAsync(notification);

            // Assert
            _mockClients.Verify(x => x.Group("Request_123"), Times.Once);
            _mockClientProxy.Verify(
                x => x.SendAsync("ReceiveGroupNotification", It.IsAny<object>(), default),
                Times.Once);
        }

        [Test]
        public async Task GetUserNotificationsAsync_ShouldReturnUserNotifications()
        {
            // Arrange
            var userId = "testuser";
            await _notificationService.SendNotificationToUserAsync(
                userId, "Test", "Test message", NotificationType.TaskAssigned);

            // Act
            var notifications = await _notificationService.GetUserNotificationsAsync(userId);

            // Assert
            Assert.That(notifications, Has.Count.EqualTo(1));
            Assert.That(notifications[0].Title, Is.EqualTo("Test"));
            Assert.That(notifications[0].Message, Is.EqualTo("Test message"));
        }

        [Test]
        public async Task GetUserNotificationsAsync_WithUnreadOnly_ShouldReturnOnlyUnread()
        {
            // Arrange
            var userId = "testuser";
            
            // Send two notifications
            await _notificationService.SendNotificationToUserAsync(
                userId, "Test1", "Message1", NotificationType.TaskAssigned);
            await _notificationService.SendNotificationToUserAsync(
                userId, "Test2", "Message2", NotificationType.TaskCompleted);

            // Mark one as read
            var allNotifications = await _notificationService.GetUserNotificationsAsync(userId);
            await _notificationService.MarkNotificationAsReadAsync(allNotifications[0].Id);

            // Act
            var unreadNotifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly: true);

            // Assert
            Assert.That(unreadNotifications, Has.Count.EqualTo(1));
            Assert.That(unreadNotifications[0].Title, Is.EqualTo("Test2"));
        }

        [Test]
        public async Task MarkAllNotificationsAsReadAsync_ShouldMarkAllAsRead()
        {
            // Arrange
            var userId = "testuser";
            await _notificationService.SendNotificationToUserAsync(
                userId, "Test1", "Message1", NotificationType.TaskAssigned);
            await _notificationService.SendNotificationToUserAsync(
                userId, "Test2", "Message2", NotificationType.TaskCompleted);

            // Act
            await _notificationService.MarkAllNotificationsAsReadAsync(userId);

            // Assert
            var unreadNotifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly: true);
            Assert.That(unreadNotifications, Is.Empty);
        }
    }
}

namespace Notification.Tests.EventHandlers
{
    [TestFixture]
    public class TaskAssignedNotificationEventHandlerTests
    {
        private Mock<INotificationService> _mockNotificationService;
        private Mock<ILogger<TaskAssignedNotificationEventHandler>> _mockLogger;
        private TaskAssignedNotificationEventHandler _handler;

        [SetUp]
        public void SetUp()
        {
            _mockNotificationService = new Mock<INotificationService>();
            _mockLogger = new Mock<ILogger<TaskAssignedNotificationEventHandler>>();
            _handler = new TaskAssignedNotificationEventHandler(
                _mockNotificationService.Object,
                _mockLogger.Object);
        }

        [Test]
        public async Task Consume_ShouldCallNotificationService()
        {
            // Arrange
            var taskAssigned = new Assignment.Events.TaskAssigned
            {
                CorrelationId = Guid.NewGuid(),
                TaskName = "Admin",
                AssignedTo = "testuser",
                AssignedType = "U"
            };

            var mockContext = new Mock<MassTransit.ConsumeContext<Assignment.Events.TaskAssigned>>();
            mockContext.Setup(x => x.Message).Returns(taskAssigned);
            mockContext.Setup(x => x.Headers).Returns(new Mock<MassTransit.Headers>().Object);

            // Act
            await _handler.Consume(mockContext.Object);

            // Assert
            _mockNotificationService.Verify(
                x => x.SendTaskAssignedNotificationAsync(It.IsAny<TaskAssignedNotificationDto>()),
                Times.Once);
        }
    }
}

namespace Notification.Tests.Integration
{
    [TestFixture]
    public class SignalRIntegrationTests
    {
        // These would be integration tests requiring a test server
        // Example structure only - requires more setup

        [Test]
        public async Task SignalRHub_ShouldAcceptConnections()
        {
            // Arrange
            // Set up test server with SignalR hub
            
            // Act
            // Connect to SignalR hub
            
            // Assert
            // Verify connection successful
        }

        [Test]
        public async Task NotificationFlow_EndToEnd_ShouldWork()
        {
            // Arrange
            // Set up test server, SignalR client, and MassTransit test harness
            
            // Act
            // Publish TaskAssigned event
            
            // Assert
            // Verify SignalR client received notification
        }
    }
}