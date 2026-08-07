---
name: analyze-complexity
description: Calculate cyclomatic complexity and other code complexity metrics
version: 1.0.0
inputs:
  - name: file_path
    type: string
    description: Path to the file or directory to analyze
    required: true
  - name: threshold
    type: number
    description: Complexity threshold for flagging problematic code
    required: false
    default: 10
outputs:
  - name: complexity_metrics
    type: object
    description: Detailed complexity metrics for each function/method
  - name: high_complexity_items
    type: array
    description: List of functions/methods exceeding the complexity threshold
tags:
  - code-analysis
  - complexity
  - metrics
  - maintainability
author: Neo Templates
last_updated: 2026-02-10
---

# Analyze Complexity

## Purpose

Calculates cyclomatic complexity and other code complexity metrics to identify difficult-to-maintain code. Helps teams prioritize refactoring efforts and maintain code quality standards.

## When to Use

- Before refactoring to identify problematic areas
- During code review to assess maintainability
- As part of regular code quality audits
- When establishing complexity budgets
- To track complexity trends over time

## Prerequisites

- Access to source code files
- Understanding of complexity metrics (cyclomatic, cognitive, Halstead)
- Baseline complexity thresholds for your project

## Inputs

### file_path
- **Type**: string
- **Required**: Yes
- **Description**: Path to the file or directory to analyze for complexity metrics
- **Example**: `src/utils/validator.js` or `src/`

### threshold
- **Type**: number
- **Required**: No
- **Default**: 10
- **Description**: Maximum acceptable cyclomatic complexity. Functions above this are flagged for refactoring.
- **Example**: `15` (more lenient) or `5` (very strict)

## Outputs

### complexity_metrics
- **Type**: object
- **Description**: Comprehensive complexity metrics for the analyzed code
- **Example**:
```json
{
  "file": "src/utils/validator.js",
  "overall_complexity": 45,
  "average_complexity": 7.5,
  "functions": [
    {
      "name": "validateUserInput",
      "start_line": 15,
      "end_line": 67,
      "cyclomatic_complexity": 18,
      "cognitive_complexity": 22,
      "lines_of_code": 52,
      "parameters": 4,
      "status": "needs_refactoring"
    }
  ]
}
```

### high_complexity_items
- **Type**: array
- **Description**: Functions or methods that exceed the complexity threshold
- **Example**:
```json
[
  {
    "function": "validateUserInput",
    "file": "src/utils/validator.js",
    "line": 15,
    "complexity": 18,
    "threshold": 10,
    "recommendation": "Split into smaller validation functions for each field type"
  }
]
```

## Implementation Steps

1. **Parse source code**
   - Read and tokenize the source file(s)
   - Build Abstract Syntax Tree (AST)
   - Identify all functions, methods, and code blocks

2. **Calculate Cyclomatic Complexity**
   - Count decision points in each function:
     - if/else statements
     - switch/case statements
     - loops (for, while, do-while)
     - logical operators (&&, ||)
     - ternary operators
     - catch blocks
   - Formula: CC = E - N + 2P
     - E = number of edges in control flow graph
     - N = number of nodes
     - P = number of connected components

3. **Calculate Cognitive Complexity** (optional)
   - Measure how difficult code is to understand
   - Penalize nested structures more heavily
   - Consider breaks in linear flow

4. **Calculate additional metrics**
   - Lines of Code (LOC)
   - Number of parameters
   - Nesting depth
   - Number of return statements
   - Halstead metrics (if applicable)

5. **Compare against thresholds**
   - Flag functions exceeding complexity threshold
   - Categorize by severity:
     - 1-10: Low complexity (good)
     - 11-20: Moderate complexity (watch)
     - 21-50: High complexity (refactor soon)
     - 50+: Very high complexity (refactor now)

6. **Generate recommendations**
   - Suggest specific refactoring strategies:
     - Extract method for nested logic
     - Use guard clauses to reduce nesting
     - Replace complex conditionals with polymorphism
     - Break down large functions

7. **Create summary report**
   - Overall file/project complexity
   - Distribution of complexity across functions
   - Trend analysis (if comparing with previous runs)
   - Priority list for refactoring

## Usage Examples

### Example 1: Analyze a single file with default threshold
```markdown
Using skill: analyze-complexity

Input:
- file_path: src/services/order-processor.js
- threshold: 10

Results:
Overall Complexity: 156
Average Complexity: 13

High Complexity Functions:
1. processOrder() - Complexity: 28 (Line 45)
   → Recommendation: Extract payment, inventory, and notification logic
   
2. calculateDiscount() - Complexity: 15 (Line 120)
   → Recommendation: Use strategy pattern for discount types

3. validateOrder() - Complexity: 12 (Line 200)
   → Recommendation: Split by validation concern (items, shipping, payment)
```

### Example 2: Strict analysis for critical code
```markdown
Using skill: analyze-complexity

Input:
- file_path: src/security/auth.js
- threshold: 5

Results:
Overall Complexity: 42
Average Complexity: 6

High Complexity Functions:
1. authenticate() - Complexity: 8 (Line 15)
   → Recommendation: Extract token validation and session management
   
2. authorizeAccess() - Complexity: 7 (Line 80)
   → Recommendation: Use permission service for role checking

Note: Security-critical code should maintain low complexity for easier auditing.
```

### Example 3: Directory-wide analysis
```markdown
Using skill: analyze-complexity

Input:
- file_path: src/components/
- threshold: 10

Summary:
Files analyzed: 24
Total functions: 156
Average complexity: 5.2

Files needing attention:
1. DataTable.jsx - Average: 12, Max: 24
2. FormValidator.jsx - Average: 11, Max: 19
3. ChartRenderer.jsx - Average: 9, Max: 15

Priority: Start with DataTable.jsx/renderRows() (complexity: 24)
```

## Complexity Guidelines

### Interpretation
- **1-10**: Simple, easy to test and maintain
- **11-20**: Moderate, may need simplification
- **21-50**: Complex, should be refactored
- **50+**: Very complex, refactor immediately

### Target Thresholds by Code Type
- **Business Logic**: 10
- **Algorithms**: 15
- **Utilities**: 8
- **Security Code**: 5
- **UI Components**: 12

## Related Skills

- `code-analysis/detect-patterns` - Identify anti-patterns causing complexity
- `refactoring/extract-function` - Reduce complexity by extracting methods
- `refactoring/simplify-conditional` - Simplify complex conditionals
- `testing/generate-unit-tests` - Complex code needs thorough testing

## Notes

- Complexity is a guide, not an absolute rule
- Domain complexity may justify higher code complexity
- Consider cognitive complexity for nested structures
- Track complexity trends over time
- Balance complexity reduction with code clarity
