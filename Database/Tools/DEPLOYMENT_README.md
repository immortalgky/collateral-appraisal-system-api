# Database Deployment Pipeline

This directory contains tools and configurations for automated database deployment across environments.

## Pipeline Components

### CI/CD Configurations
- **azure-pipelines-database.yml** - Azure DevOps pipeline configuration
- **templates/deploy-database.yml** - Reusable deployment template
- **.github/workflows/database-deployment.yml** - GitHub Actions workflow

### Management Scripts

**Cross-platform (Bash):**
- **validate-pipeline-basic.sh** - Basic pipeline validation (no DB required)
- **test-pipeline.sh** - Full pipeline validation and testing  
- **validate-sql-scripts.sh** - SQL script validation and linting

**PowerShell (Windows/PowerShell Core):**
- **RollbackScript.ps1** - Automated rollback with safety checks
- **DeploymentMonitoring.ps1** - Monitoring and alerting for deployments
- **TestPipeline.ps1** - Pipeline validation and testing
- **ValidateScripts.ps1** - SQL script validation and linting

## Environment Setup

### Required Environment Variables
```bash
# Development
export DEV_CONNECTION_STRING="Server=(localdb)\mssqllocaldb;Database=CollateralAppraisalSystem_Dev;Trusted_Connection=true"

# Test
export TEST_CONNECTION_STRING="Server=test-server;Database=CollateralAppraisalSystem_Test;..."

# Staging
export STAGING_CONNECTION_STRING="Server=staging-server;Database=CollateralAppraisalSystem_Staging;..."

# Production
export PRODUCTION_CONNECTION_STRING="Server=prod-server;Database=CollateralAppraisalSystem;..."
```

### Azure DevOps Setup
1. Create variable group: `Database-Deployment-Variables`
2. Add connection strings as secure variables
3. Configure environment approvals
4. Set up notification webhooks

### GitHub Actions Setup
1. Add connection strings to repository secrets
2. Configure environment protection rules
3. Set up required reviewers for production

## Usage Examples

### Deploy to Development
```bash
dotnet run --project Database/Database.csproj migrate Development
```

### Validate SQL Scripts
```bash
# Cross-platform validation
./Database/Tools/validate-sql-scripts.sh Database/Scripts
```

### Validate Pipeline

**Cross-platform (Bash) - Recommended:**
```bash
# Basic validation (no database connections required)
./Database/Tools/validate-pipeline-basic.sh

# Full validation with database tests
./Database/Tools/test-pipeline.sh Development Test
```

**PowerShell (if available):**
```powershell
./Database/Tools/TestPipeline.ps1 -Environments @("Development", "Test")
```

### Emergency Rollback
```powershell
./Database/Tools/RollbackScript.ps1 -Environment Production -TargetVersion "1.2.0" -Force
```

### Monitor Deployments
```powershell
./Database/Tools/DeploymentMonitoring.ps1 -Environment Production -WebhookUrl "https://hooks.slack.com/..."
```

## Deployment Flow

1. **Development** - Automatic on develop branch
2. **Test** - Automatic after dev success
3. **Staging** - Manual approval required (main branch)
4. **Production** - Manual approval with health checks

## Safety Features

- Automatic database backups before deployments
- Pre-deployment validation checks
- Post-deployment verification
- Rollback automation with confirmation prompts
- Monitoring and alerting for failures
- Environment-specific approval gates

## Troubleshooting

### Common Issues
1. **Connection failures** - Check connection strings and network access
2. **Permission errors** - Verify database user permissions
3. **Timeout errors** - Increase CommandTimeout for large deployments
4. **Rollback failures** - Check backup availability and script compatibility

### Emergency Procedures
1. **Failed deployment** - Use RollbackScript.ps1 with appropriate version
2. **Data corruption** - Restore from pre-deployment backup
3. **Performance issues** - Monitor with DeploymentMonitoring.ps1
4. **Pipeline failures** - Check TestPipeline.ps1 validation results

## Monitoring and Alerts

The deployment pipeline includes comprehensive monitoring:
- Real-time deployment status
- Performance metrics
- Error notifications
- Health check validations
- Audit trail maintenance

Configure webhook URLs in your CI/CD platform to receive notifications on:
- Deployment successes/failures
- Long-running operations
- Health check alerts
- Rollback events