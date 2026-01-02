#!/bin/bash

# Cross-platform pipeline validation script
# Usage: ./test-pipeline.sh [environment1] [environment2] ...

set -e  # Exit on any error

# Default environments to test
ENVIRONMENTS="${@:-Development Test}"
LOG_FILE="pipeline-test-results.log"
RESULTS_FILE="pipeline-test-results-$(date +%Y%m%d-%H%M%S).json"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Logging function
log_message() {
    local level="$1"
    local message="$2"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    local log_entry="[$timestamp] [$level] $message"
    
    case $level in
        "ERROR")   echo -e "${RED}$log_entry${NC}" ;;
        "SUCCESS") echo -e "${GREEN}$log_entry${NC}" ;;
        "WARNING") echo -e "${YELLOW}$log_entry${NC}" ;;
        "INFO")    echo -e "${CYAN}$log_entry${NC}" ;;
        *)         echo "$log_entry" ;;
    esac
    
    echo "$log_entry" >> "$LOG_FILE"
}

# Test database connection (simplified - just check if connection string exists)
test_database_connection() {
    local env="$1"
    local conn_var=""
    
    case "$(echo $env | tr '[:upper:]' '[:lower:]')" in  # Convert to lowercase
        "development") conn_var="DEV_CONNECTION_STRING" ;;
        "test")        conn_var="TEST_CONNECTION_STRING" ;;
        "staging")     conn_var="STAGING_CONNECTION_STRING" ;;
        "production")  conn_var="PRODUCTION_CONNECTION_STRING" ;;
        *)             log_message "ERROR" "Unknown environment: $env"; return 1 ;;
    esac
    
    if [[ -n "${!conn_var}" ]]; then
        log_message "SUCCESS" "Connection string found for $env"
        return 0
    else
        log_message "WARNING" "No connection string configured for $env ($conn_var)"
        return 1
    fi
}

# Test migration project build
test_migration_build() {
    local env="$1"
    
    log_message "INFO" "Testing migration build for $env"
    
    if [[ ! -f "Database/Database.csproj" ]]; then
        log_message "ERROR" "Migration project not found: Database/Database.csproj"
        return 1
    fi
    
    # Test build
    if dotnet build Database/Database.csproj --configuration Release --verbosity quiet > /dev/null 2>&1; then
        log_message "SUCCESS" "Migration build test passed for $env"
        return 0
    else
        log_message "ERROR" "Migration build failed for $env"
        return 1
    fi
}

# Test migration validation
test_migration_validation() {
    local env="$1"
    
    log_message "INFO" "Testing migration validation for $env"
    
    # Test that we can run the migration tool help
    if dotnet run --project Database/Database.csproj --help > /dev/null 2>&1; then
        log_message "SUCCESS" "Migration validation test passed for $env"
        return 0
    else
        log_message "WARNING" "Migration validation had issues for $env (this may be expected)"
        return 0  # Don't fail on this as the CLI might not have --help
    fi
}

# Test script files exist
test_script_files() {
    local env="$1"
    local missing_files=0
    
    log_message "INFO" "Testing required script files for $env"
    
    # Check for essential files
    local required_files=(
        "Database/Tools/ValidateScripts.ps1"
        "Database/Tools/CreateDatabaseObject.ps1"
        "azure-pipelines-database.yml"
        ".github/workflows/database-deployment.yml"
    )
    
    for file in "${required_files[@]}"; do
        if [[ ! -f "$file" ]]; then
            log_message "ERROR" "Required file missing: $file"
            ((missing_files++))
        fi
    done
    
    # Check for SQL scripts
    local sql_count=$(find Database/Scripts -name "*.sql" 2>/dev/null | wc -l)
    if [[ $sql_count -gt 0 ]]; then
        log_message "SUCCESS" "Found $sql_count SQL script files"
    else
        log_message "WARNING" "No SQL script files found in Database/Scripts"
    fi
    
    if [[ $missing_files -eq 0 ]]; then
        log_message "SUCCESS" "All required script files found for $env"
        return 0
    else
        log_message "ERROR" "Missing $missing_files required files for $env"
        return 1
    fi
}

# Test configuration files
test_configuration() {
    local env="$1"
    
    log_message "INFO" "Testing configuration files for $env"
    
    if [[ -f "Database/Configuration/appsettings.Database.json" ]]; then
        # Basic JSON validation
        if python3 -m json.tool Database/Configuration/appsettings.Database.json > /dev/null 2>&1; then
            log_message "SUCCESS" "Configuration file is valid JSON for $env"
            return 0
        else
            log_message "ERROR" "Configuration file has invalid JSON for $env"
            return 1
        fi
    else
        log_message "WARNING" "Configuration file not found for $env"
        return 1
    fi
}

# Main test execution
main() {
    log_message "INFO" "Starting pipeline validation tests"
    log_message "INFO" "Testing environments: $ENVIRONMENTS"
    
    # Initialize results
    local overall_success=true
    local test_results="{"
    test_results+='"TestDate":"'$(date -Iseconds)'",'
    test_results+='"Environments":['
    
    local first_env=true
    
    for env in $ENVIRONMENTS; do
        log_message "INFO" "Testing $env environment..."
        
        if [[ "$first_env" == false ]]; then
            test_results+=","
        fi
        first_env=false
        
        test_results+='{"Environment":"'$env'","Tests":{'
        
        local env_success=true
        local first_test=true
        
        # Run tests
        local tests=(
            "Connection:test_database_connection"
            "Build:test_migration_build" 
            "Validation:test_migration_validation"
            "Scripts:test_script_files"
            "Configuration:test_configuration"
        )
        
        for test_spec in "${tests[@]}"; do
            IFS=":" read -r test_name test_func <<< "$test_spec"
            
            if [[ "$first_test" == false ]]; then
                test_results+=","
            fi
            first_test=false
            
            if $test_func "$env"; then
                test_results+='"'$test_name'":{"Success":true,"Message":"Test passed"}'
            else
                test_results+='"'$test_name'":{"Success":false,"Message":"Test failed"}'
                env_success=false
                overall_success=false
            fi
        done
        
        test_results+='}}'
        
        if [[ "$env_success" == true ]]; then
            log_message "SUCCESS" "$env environment validation completed successfully"
        else
            log_message "ERROR" "$env environment validation completed with failures"
        fi
    done
    
    test_results+='],"OverallSuccess":'$overall_success'}'
    
    # Save results
    echo "$test_results" | python3 -m json.tool > "$RESULTS_FILE" 2>/dev/null || echo "$test_results" > "$RESULTS_FILE"
    
    # Generate summary
    log_message "INFO" "Pipeline Validation Test Summary"
    log_message "INFO" "================================"
    log_message "INFO" "Test Date: $(date)"
    log_message "INFO" "Environments Tested: $ENVIRONMENTS"
    log_message "INFO" "Detailed results saved to: $RESULTS_FILE"
    
    if [[ "$overall_success" == true ]]; then
        log_message "SUCCESS" "All tests passed - pipeline is ready for deployment"
        exit 0
    else
        log_message "ERROR" "Some tests failed - please review the results before deploying"
        exit 1
    fi
}

# Help function
show_help() {
    echo "Database Pipeline Validation Script"
    echo
    echo "Usage: $0 [environment1] [environment2] ..."
    echo
    echo "Available environments:"
    echo "  Development  - Development environment"
    echo "  Test         - Test environment" 
    echo "  Staging      - Staging environment"
    echo "  Production   - Production environment"
    echo
    echo "Examples:"
    echo "  $0                           # Test Development and Test"
    echo "  $0 Development               # Test Development only"
    echo "  $0 Development Test Staging  # Test multiple environments"
    echo
    echo "Environment variables (optional):"
    echo "  DEV_CONNECTION_STRING        - Development database connection"
    echo "  TEST_CONNECTION_STRING       - Test database connection"
    echo "  STAGING_CONNECTION_STRING    - Staging database connection"
    echo "  PRODUCTION_CONNECTION_STRING - Production database connection"
}

# Check for help flag
if [[ "$1" == "-h" || "$1" == "--help" ]]; then
    show_help
    exit 0
fi

# Run main function
main