# Workflow System Documentation

## Main Documentation

📖 **[Workflow System Guide](workflow-system-guide.md)** - Complete developer guide covering architecture, implementation, and usage

## Specialized Guides

🔧 **[Advanced Workflow Engine](advanced-workflow-engine.md)** - Technical details on expressions and Fork/Join activities

📝 **[Expression Examples](workflow-expression-examples.md)** - Practical examples of expression usage in workflows

🧪 **[Testing Guide](workflow-testing-guide.md)** - Comprehensive testing strategies for workflow features

## Quick Reference

### Key Concepts
- **Activity-Managed Execution**: Activities control their own lifecycle
- **Assignment Strategies**: RoundRobin, Random, WorkloadBased, Manual
- **Input/Output Mapping**: Configurable data flow between activities and workflow
- **Expression Engine**: Advanced conditional logic for transitions

### Common Activity Types
- **TaskActivity**: User assignment and manual task completion
- **DecisionActivity**: Conditional routing based on expressions
- **ForkActivity/JoinActivity**: Parallel execution and synchronization
- **StartActivity/EndActivity**: Workflow boundary activities

### API Endpoints
- `POST /api/workflows/instances` - Start workflow
- `POST /api/workflows/instances/{id}/activities/{activityId}/complete` - Resume workflow

For detailed implementation guidance, start with the [Workflow System Guide](workflow-system-guide.md).