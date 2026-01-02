#!/bin/bash

# Cross-platform SQL script validation
# Usage: ./validate-sql-scripts.sh [scripts-path]

SCRIPTS_PATH="${1:-Database/Scripts}"
WARNINGS=0
ERRORS=0

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${CYAN}🔍 Validating SQL scripts in: $SCRIPTS_PATH${NC}"
echo "=================================="

# Find all SQL files
SQL_FILES=($(find "$SCRIPTS_PATH" -name "*.sql" -type f 2>/dev/null))

if [[ ${#SQL_FILES[@]} -eq 0 ]]; then
    echo -e "${RED}❌ No SQL files found in $SCRIPTS_PATH${NC}"
    exit 1
fi

echo -e "${CYAN}Found ${#SQL_FILES[@]} SQL files to validate${NC}"
echo

for file in "${SQL_FILES[@]}"; do
    echo -n "Validating $(basename "$file")... "
    
    ISSUES=()
    
    # Read file content
    CONTENT=$(cat "$file")
    
    # Basic syntax checks
    if echo "$CONTENT" | grep -q "SELECT \*" && [[ "$file" == *"View"* ]]; then
        ISSUES+=("WARNING: SELECT * found in view")
        ((WARNINGS++))
    fi
    
    if ! echo "$CONTENT" | grep -q "GO\s*$"; then
        ISSUES+=("WARNING: Missing GO statement at end")
        ((WARNINGS++))
    fi
    
    if ! echo "$CONTENT" | grep -q "GRANT\s\+\(SELECT\|EXECUTE\)"; then
        ISSUES+=("WARNING: Missing permission grants")
        ((WARNINGS++))
    fi
    
    if echo "$CONTENT" | grep -q "SET NOCOUNT ON" && [[ "$file" != *"StoredProcedure"* ]]; then
        ISSUES+=("INFO: SET NOCOUNT ON in non-procedure")
    fi
    
    # Check for common SQL injection patterns (basic)
    if echo "$CONTENT" | grep -q "'+.*+'" && ! echo "$CONTENT" | grep -q "@"; then
        ISSUES+=("WARNING: Potential SQL injection risk - consider parameterized queries")
        ((WARNINGS++))
    fi
    
    # Check for proper schema references
    if ! echo "$CONTENT" | grep -q "\[.*\]\.\[.*\]"; then
        ISSUES+=("INFO: Consider using schema-qualified object names")
    fi
    
    # Display results
    if [[ ${#ISSUES[@]} -eq 0 ]]; then
        echo -e "${GREEN}✅${NC}"
    else
        echo -e "${YELLOW}⚠️${NC}"
        for issue in "${ISSUES[@]}"; do
            if [[ "$issue" == WARNING* ]]; then
                echo -e "    ${YELLOW}$issue${NC}"
            elif [[ "$issue" == ERROR* ]]; then
                echo -e "    ${RED}$issue${NC}"
            else
                echo -e "    ${CYAN}$issue${NC}"
            fi
        done
    fi
done

echo
echo "📋 Validation Summary:"
echo "====================="
echo -e "Files processed: ${CYAN}${#SQL_FILES[@]}${NC}"
echo -e "Warnings: ${YELLOW}$WARNINGS${NC}"
echo -e "Errors: ${RED}$ERRORS${NC}"

if [[ $ERRORS -gt 0 ]]; then
    echo -e "\n${RED}❌ Validation failed with errors${NC}"
    exit 1
elif [[ $WARNINGS -gt 0 ]]; then
    echo -e "\n${YELLOW}⚠️  Validation completed with warnings${NC}"
    exit 0
else
    echo -e "\n${GREEN}✅ All SQL scripts validated successfully${NC}"
    exit 0
fi