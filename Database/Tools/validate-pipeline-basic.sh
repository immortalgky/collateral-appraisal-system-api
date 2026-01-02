#!/bin/bash

# Basic pipeline validation script (no database connections required)
# Usage: ./validate-pipeline-basic.sh

set -e

echo "🔍 Basic Pipeline Validation"
echo "============================"

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

PASSED=0
FAILED=0

# Test function
test_check() {
    local test_name="$1"
    local test_command="$2"
    
    echo -n "Testing $test_name... "
    
    if eval "$test_command" > /dev/null 2>&1; then
        echo -e "${GREEN}✅ PASS${NC}"
        ((PASSED++))
        return 0
    else
        echo -e "${RED}❌ FAIL${NC}"
        ((FAILED++))
        return 1
    fi
}

echo
echo "📁 File Structure Tests:"
echo "------------------------"

test_check "Database project exists" "test -f Database/Database.csproj"
test_check "Azure DevOps pipeline" "test -f azure-pipelines-database.yml"
test_check "GitHub Actions workflow" "test -f .github/workflows/database-deployment.yml"
test_check "Deployment template" "test -f templates/deploy-database.yml"
test_check "Configuration file" "test -f Database/Configuration/appsettings.Database.json"

echo
echo "🛠️  Tool Scripts Tests:"
echo "----------------------"

test_check "Validation script (PowerShell)" "test -f Database/Tools/ValidateScripts.ps1"
test_check "Rollback script (PowerShell)" "test -f Database/Tools/RollbackScript.ps1"
test_check "Monitoring script (PowerShell)" "test -f Database/Tools/DeploymentMonitoring.ps1"
test_check "Pipeline test script (PowerShell)" "test -f Database/Tools/TestPipeline.ps1"
test_check "Object creation script (PowerShell)" "test -f Database/Tools/CreateDatabaseObject.ps1"
test_check "Pipeline test script (Bash)" "test -f Database/Tools/test-pipeline.sh"
test_check "Basic validation script (Bash)" "test -f Database/Tools/validate-pipeline-basic.sh"

echo
echo "📄 SQL Scripts Tests:"
echo "--------------------"

SQL_COUNT=$(find Database/Scripts -name "*.sql" 2>/dev/null | wc -l | tr -d ' ')
if [[ $SQL_COUNT -gt 0 ]]; then
    echo -e "SQL script files: ${GREEN}✅ Found $SQL_COUNT files${NC}"
    ((PASSED++))
else
    echo -e "SQL script files: ${RED}❌ No files found${NC}"
    ((FAILED++))
fi

# Check for specific module scripts
MODULES=("Request" "Document" "Assignment")
for module in "${MODULES[@]}"; do
    VIEW_COUNT=$(find "Database/Scripts/Views/$module" -name "*.sql" 2>/dev/null | wc -l | tr -d ' ')
    if [[ $VIEW_COUNT -gt 0 ]]; then
        echo -e "$module views: ${GREEN}✅ $VIEW_COUNT files${NC}"
        ((PASSED++))
    else
        echo -e "$module views: ${YELLOW}⚠️  No files${NC}"
    fi
done

echo
echo "🏗️  Build Tests:"
echo "---------------"

test_check "Database project builds" "dotnet build Database/Database.csproj --configuration Release"
test_check "Solution builds" "dotnet build --configuration Release"

echo
echo "📋 Configuration Tests:"
echo "----------------------"

# Test JSON validity
if command -v python3 > /dev/null 2>&1; then
    test_check "Configuration JSON validity" "python3 -m json.tool Database/Configuration/appsettings.Database.json"
else
    echo -e "JSON validation: ${YELLOW}⚠️  Python3 not available${NC}"
fi

# Test YAML validity (basic)
test_check "Azure DevOps YAML syntax" "grep -q 'trigger:' azure-pipelines-database.yml"
test_check "GitHub Actions YAML syntax" "grep -q 'on:' .github/workflows/database-deployment.yml"

echo
echo "📊 Results Summary:"
echo "==================="
echo -e "Tests Passed: ${GREEN}$PASSED${NC}"
echo -e "Tests Failed: ${RED}$FAILED${NC}"
echo "Total Tests: $((PASSED + FAILED))"

if [[ $FAILED -eq 0 ]]; then
    echo -e "\n${GREEN}🎉 All tests passed! Pipeline is ready.${NC}"
    exit 0
else
    echo -e "\n${RED}⚠️  Some tests failed. Please review and fix issues.${NC}"
    exit 1
fi