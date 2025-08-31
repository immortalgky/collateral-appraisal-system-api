#!/bin/bash

# Quick test script for real-time notifications
# Make executable with: chmod +x test_notifications.sh

BASE_URL="https://localhost:7111"

echo "🚀 Testing Real-time Notification System"
echo "======================================="

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to make API calls
api_call() {
    local method=$1
    local endpoint=$2
    local data=$3
    
    echo -e "${BLUE}$method $endpoint${NC}"
    
    if [ -n "$data" ]; then
        response=$(curl -s -k -X "$method" "$BASE_URL$endpoint" \
            -H "Content-Type: application/json" \
            -d "$data")
    else
        response=$(curl -s -k -X "$method" "$BASE_URL$endpoint" \
            -H "accept: application/json")
    fi
    
    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✅ Success${NC}"
        echo "$response" | jq '.' 2>/dev/null || echo "$response"
    else
        echo -e "${RED}❌ Failed${NC}"
    fi
    echo ""
}

# Test 1: Health check
echo -e "${YELLOW}1. Testing API Health...${NC}"
api_call "GET" "/health" ""

# Test 2: Simulate Task Assignment
echo -e "${YELLOW}2. Simulating Task Assignment...${NC}"
api_call "POST" "/api/test/notifications/task-assigned" '{
    "correlationId": "123e4567-e89b-12d3-a456-426614174000",
    "taskName": "Admin",
    "assignedTo": "testuser",
    "assignedType": "U"
}'

# Test 3: Simulate Task Completion
echo -e "${YELLOW}3. Simulating Task Completion...${NC}"
api_call "POST" "/api/test/notifications/task-completed" '{
    "correlationId": "123e4567-e89b-12d3-a456-426614174001",
    "taskName": "Admin",
    "actionTaken": "P"
}'

# Test 4: Simulate Workflow Transition
echo -e "${YELLOW}4. Simulating Workflow Transition...${NC}"
api_call "POST" "/api/test/notifications/transition-completed" '{
    "correlationId": "123e4567-e89b-12d3-a456-426614174002",
    "requestId": 1,
    "taskName": "AppraisalStaff",
    "currentState": "AppraisalStaff",
    "assignedTo": "appraiser1",
    "assignedType": "U"
}'

# Test 5: Get User Notifications
echo -e "${YELLOW}5. Getting User Notifications...${NC}"
api_call "GET" "/api/notifications/testuser" ""

# Test 6: Get Workflow Status
echo -e "${YELLOW}6. Getting Workflow Status...${NC}"
api_call "GET" "/api/workflow/1/status" ""

# Test 7: Mark All Notifications as Read
echo -e "${YELLOW}7. Marking All Notifications as Read...${NC}"
api_call "PATCH" "/api/notifications/users/testuser/read-all" ""

echo -e "${GREEN}🎉 Test completed!${NC}"
echo ""
echo -e "${BLUE}📝 Next Steps:${NC}"
echo "1. Open SIGNALR_TEST.html in your browser to test real-time functionality"
echo "2. Check the application logs for notification processing"
echo "3. Monitor SignalR connections in browser dev tools"
echo ""
echo -e "${YELLOW}💡 Tip:${NC} Run 'dotnet run --project Bootstrapper/Api/Api.csproj' first if not already running"