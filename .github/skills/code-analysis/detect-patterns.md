---
name: detect-patterns
description: Identify common code patterns, anti-patterns, and design patterns in source code
version: 1.0.0
inputs:
  - name: file_path
    type: string
    description: Path to the file or directory to analyze
    required: true
  - name: pattern_types
    type: array
    description: Types of patterns to detect (design-patterns, anti-patterns, code-smells, all)
    required: false
    default: all
outputs:
  - name: patterns_found
    type: array
    description: List of detected patterns with locations and descriptions
  - name: recommendations
    type: array
    description: Suggested improvements based on detected patterns
tags:
  - code-analysis
  - patterns
  - best-practices
author: Neo Templates
last_updated: 2026-02-10
---

# Detect Patterns

## Purpose

Analyzes source code to identify design patterns, anti-patterns, code smells, and common coding patterns. Helps developers understand code structure and identify opportunities for improvement or refactoring.

## When to Use

- During code review to identify potential issues
- Before refactoring to understand current code structure
- When analyzing unfamiliar codebases
- To validate adherence to design patterns
- Before planning architectural changes

## Prerequisites

- Access to source code files
- Understanding of common design patterns and anti-patterns
- Context about the programming language and framework being used

## Inputs

### file_path
- **Type**: string
- **Required**: Yes
- **Description**: The path to the file or directory to analyze. Can be a single file or a directory for recursive analysis.
- **Example**: `src/services/auth.js` or `src/components/`

### pattern_types
- **Type**: array
- **Required**: No
- **Default**: `["all"]`
- **Description**: Specifies which types of patterns to detect. Options: design-patterns, anti-patterns, code-smells, all
- **Example**: `["anti-patterns", "code-smells"]`

## Outputs

### patterns_found
- **Type**: array
- **Description**: List of detected patterns with their type, location, description, and severity
- **Example**: 
```json
[
  {
    "type": "Singleton Pattern",
    "category": "design-pattern",
    "file": "src/config/database.js",
    "line": 15,
    "description": "Database connection uses singleton pattern"
  },
  {
    "type": "God Object",
    "category": "anti-pattern",
    "file": "src/utils/helper.js",
    "line": 1,
    "severity": "high",
    "description": "Class has too many responsibilities"
  }
]
```

### recommendations
- **Type**: array
- **Description**: Actionable recommendations for addressing anti-patterns or improving code
- **Example**:
```json
[
  {
    "pattern": "God Object",
    "file": "src/utils/helper.js",
    "recommendation": "Split into focused utility classes (DateUtils, StringUtils, ValidationUtils)",
    "priority": "high"
  }
]
```

## Implementation Steps

1. **Parse the source code**
   - Read the specified file(s)
   - Parse into AST (Abstract Syntax Tree) if needed
   - Identify code structure (classes, functions, modules)

2. **Detect design patterns**
   - Look for common patterns: Singleton, Factory, Observer, Strategy, etc.
   - Identify pattern implementations
   - Note pattern locations and usage

3. **Identify anti-patterns**
   - God Object/Class: Classes with too many responsibilities
   - Spaghetti Code: Tangled control flow
   - Magic Numbers: Hard-coded values without explanation
   - Copy-Paste Programming: Duplicated code blocks
   - Callback Hell: Deep nesting of callbacks/promises
   - Tight Coupling: Excessive dependencies between modules

4. **Detect code smells**
   - Long methods (>50 lines)
   - Large classes (>300 lines)
   - Long parameter lists (>5 parameters)
   - Duplicate code
   - Dead code (unused variables, functions)
   - Complex conditionals
   - Excessive comments (may indicate unclear code)

5. **Analyze severity and impact**
   - Categorize findings by severity (low, medium, high, critical)
   - Consider maintainability impact
   - Assess performance implications

6. **Generate recommendations**
   - For each anti-pattern or code smell, provide specific refactoring suggestions
   - Prioritize based on severity and impact
   - Include references to better patterns or practices

7. **Format and return results**
   - Organize patterns by category and file
   - Include line numbers and code snippets
   - Provide actionable next steps

## Usage Examples

### Example 1: Analyze a single file
```markdown
Using skill: detect-patterns

Input:
- file_path: src/services/user-service.js
- pattern_types: ["all"]

Findings:
- Singleton pattern detected in UserService (line 10)
- God Object anti-pattern: UserService handles authentication, validation, and database operations
- Code smell: Method getUserWithAllDetails() is 85 lines long

Recommendations:
1. Split UserService into UserAuth, UserValidator, and UserRepository
2. Extract getUserWithAllDetails() into smaller, focused methods
```

### Example 2: Analyze a directory for anti-patterns only
```markdown
Using skill: detect-patterns

Input:
- file_path: src/components/
- pattern_types: ["anti-patterns", "code-smells"]

Findings:
- 5 instances of duplicate code across Button.jsx and Link.jsx
- Deep nesting (6 levels) in FormValidator.jsx
- Magic numbers found in 12 files

Recommendations:
1. Extract common button/link styles into shared component
2. Refactor FormValidator.jsx using early returns to reduce nesting
3. Create constants file for magic numbers
```

## Related Skills

- `code-analysis/analyze-complexity` - Measure cyclomatic complexity
- `code-analysis/find-dependencies` - Map module dependencies
- `refactoring/extract-function` - Refactor based on detected patterns
- `refactoring/rename-symbol` - Improve naming based on pattern analysis

## Notes

- Language-specific patterns may require different detection strategies
- Consider framework-specific patterns (React hooks, Angular services, etc.)
- Some patterns may be intentional and appropriate for the context
- Always validate findings with domain knowledge before refactoring
