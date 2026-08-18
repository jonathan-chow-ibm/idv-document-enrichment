---
name: validate-test-coverage
description: Analyze test coverage and identify untested code paths
version: 1.0.0
inputs:
  - name: source_path
    type: string
    description: Path to source code to validate coverage for
    required: true
  - name: minimum_coverage
    type: number
    description: Minimum acceptable coverage percentage
    required: false
    default: 80
  - name: coverage_type
    type: string
    description: Type of coverage to check (line, branch, function, statement)
    required: false
    default: all
outputs:
  - name: coverage_report
    type: object
    description: Detailed coverage metrics
  - name: uncovered_lines
    type: array
    description: List of uncovered code lines and branches
  - name: passes_threshold
    type: boolean
    description: Whether coverage meets minimum requirement
tags:
  - testing
  - coverage
  - quality-assurance
  - validation
author: Neo Templates
last_updated: 2026-02-10
---

# Validate Test Coverage

## Purpose

Analyzes test coverage metrics to ensure code is adequately tested. Identifies untested code paths, validates coverage thresholds, and helps maintain quality standards.

## When to Use

- Before merging code to verify adequate testing
- As part of CI/CD quality gates
- During code review to identify testing gaps
- When establishing or enforcing coverage policies
- After refactoring to ensure test completeness

## Prerequisites

- Test suite exists for the code
- Coverage tool installed (Jest, Istanbul, Coverage.py, JaCoCo, etc.)
- Tests can be executed successfully
- Coverage reporting configured

## Inputs

### source_path
- **Type**: string
- **Required**: Yes
- **Description**: Path to source code file or directory to validate coverage for
- **Example**: `src/services/` or `src/utils/auth.js`

### minimum_coverage
- **Type**: number
- **Required**: No
- **Default**: 80
- **Description**: Minimum acceptable coverage percentage (0-100)
- **Example**: `90` for critical code, `70` for less critical

### coverage_type
- **Type**: string
- **Required**: No
- **Default**: `all`
- **Description**: Type of coverage to validate: line, branch, function, statement, or all
- **Example**: `branch` (most stringent), `line` (common baseline)

## Outputs

### coverage_report
- **Type**: object
- **Description**: Comprehensive coverage metrics
- **Example**:
```json
{
  "overall": {
    "line_coverage": 85.4,
    "branch_coverage": 78.2,
    "function_coverage": 90.1,
    "statement_coverage": 84.8
  },
  "by_file": {
    "src/auth.js": {
      "line_coverage": 95.0,
      "branch_coverage": 88.0,
      "status": "passing"
    },
    "src/validator.js": {
      "line_coverage": 65.0,
      "branch_coverage": 55.0,
      "status": "failing"
    }
  }
}
```

### uncovered_lines
- **Type**: array
- **Description**: Specific lines and branches not covered by tests
- **Example**:
```json
[
  {
    "file": "src/validator.js",
    "line": 45,
    "type": "branch",
    "description": "Error handling path not tested",
    "code": "if (value === null) throw new Error()"
  },
  {
    "file": "src/validator.js",
    "line": 78,
    "type": "line",
    "description": "Default case not covered",
    "code": "return defaultValue;"
  }
]
```

### passes_threshold
- **Type**: boolean
- **Description**: True if coverage meets or exceeds minimum_coverage threshold
- **Example**: `true` if coverage is 85% and threshold is 80%

## Implementation Steps

1. **Run test suite with coverage**
   ```bash
   # JavaScript/Jest
   npm test -- --coverage
   
   # Python
   pytest --cov=src --cov-report=json
   
   # Java
   mvn test jacoco:report
   ```

2. **Parse coverage report**
   - Load coverage data (typically JSON, LCOV, or XML format)
   - Extract metrics by file and overall
   - Identify coverage types available

3. **Calculate coverage metrics**
   - **Line coverage**: % of executable lines executed by tests
   - **Branch coverage**: % of conditional branches taken
   - **Function coverage**: % of functions called
   - **Statement coverage**: % of statements executed

4. **Compare against thresholds**
   - Check overall coverage vs. minimum_coverage
   - Identify files below threshold
   - Flag critical files with low coverage

5. **Identify uncovered code**
   - Extract line numbers with zero coverage
   - Identify untested branches (if/else, switch cases)
   - Find untested functions
   - Highlight critical paths without coverage

6. **Categorize gaps by priority**
   - **Critical**: Error handling, security, data validation
   - **High**: Main business logic flows
   - **Medium**: Helper functions, utilities
   - **Low**: Trivial getters/setters, constants

7. **Generate actionable report**
   - List files below threshold
   - Show specific uncovered lines with context
   - Suggest test cases to add
   - Provide coverage trends (if historical data available)

8. **Set exit code based on threshold**
   - Return success if coverage meets minimum
   - Return failure if coverage is below threshold
   - Useful for CI/CD gates

## Usage Examples

### Example 1: Validate coverage before merge
```markdown
Using skill: validate-test-coverage

Input:
- source_path: src/
- minimum_coverage: 80
- coverage_type: all

Results:
Overall Coverage: 
  Lines: 85% ✓
  Branches: 76% ✗
  Functions: 92% ✓
  Statements: 84% ✓

Status: FAILING (branch coverage below 80%)

Files below threshold:
1. src/utils/validator.js - 65% branch coverage
   - Line 45: Error path not tested
   - Line 78: Default case not covered

2. src/services/payment.js - 72% branch coverage
   - Line 120: Refund flow not tested
   - Line 155: Timeout handling not tested

Recommendation: Add tests for error paths and edge cases
```

### Example 2: Strict validation for critical code
```markdown
Using skill: validate-test-coverage

Input:
- source_path: src/security/auth.js
- minimum_coverage: 95
- coverage_type: branch

Results:
Branch Coverage: 88% ✗

Status: FAILING (below 95% threshold)

Uncovered branches:
1. Line 34: Token expiration edge case
   if (token.exp < Date.now() - GRACE_PERIOD)
   
2. Line 67: Concurrent login handling
   if (activeSession && !options.allowMultiple)

Recommendation: Security-critical code requires 95%+ branch coverage.
Add tests for:
- Token expiration with grace period
- Multiple simultaneous login attempts
```

### Example 3: Coverage validation per coverage type
```markdown
Using skill: validate-test-coverage

Input:
- source_path: src/components/
- minimum_coverage: 70
- coverage_type: line

Results:
Line Coverage: 82% ✓

Status: PASSING

Coverage breakdown:
- Button.jsx: 95% ✓
- Form.jsx: 88% ✓
- DataTable.jsx: 65% ✗
- Chart.jsx: 78% ✓

Note: While overall coverage passes, DataTable.jsx is below threshold.
Consider adding tests for DataTable rendering edge cases.
```

## Coverage Guidelines

### Minimum Coverage Targets by Code Type
- **Security/Authentication**: 95%+
- **Payment/Financial**: 95%+
- **Business Logic**: 85-90%
- **API Endpoints**: 85%+
- **Utilities**: 80%+
- **UI Components**: 70-80%
- **Configuration**: 60-70%

### Coverage Types Importance
1. **Branch Coverage** - Most important (tests decision paths)
2. **Line Coverage** - Good baseline metric
3. **Function Coverage** - Ensures all functions are called
4. **Statement Coverage** - Similar to line coverage

### When 100% Coverage Isn't Practical
- Trivial getters/setters
- Generated code
- Third-party library wrappers
- Platform-specific code
- Defensive programming checks

## Validation Checklist

- [ ] All critical paths have tests
- [ ] Error handling is tested
- [ ] Edge cases are covered
- [ ] Happy paths are tested
- [ ] Boundary conditions are validated
- [ ] Security-critical code is thoroughly tested
- [ ] Recent changes have test coverage
- [ ] Coverage hasn't decreased from previous version

## Related Skills

- `testing/generate-unit-tests` - Create tests to improve coverage
- `testing/generate-integration-tests` - Add integration test coverage
- `code-analysis/analyze-complexity` - Complex code needs better coverage
- `code-analysis/detect-patterns` - Identify untested patterns

## Notes

- Coverage is a safety net, not a goal in itself
- High coverage doesn't guarantee no bugs
- Focus on meaningful tests, not just coverage percentage
- Test edge cases and error paths, not just happy paths
- Review coverage trends over time
- Use coverage reports to guide testing efforts
- Consider mutation testing for additional validation
- Exclude generated code from coverage requirements
