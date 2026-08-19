---
name: extract-function
description: Extract duplicate or complex code into reusable functions or methods
version: 1.0.0
inputs:
  - name: source_file
    type: string
    description: Path to the file containing code to refactor
    required: true
  - name: target_code
    type: string
    description: Code block to extract (line range or code snippet)
    required: true
  - name: function_name
    type: string
    description: Suggested name for the extracted function
    required: false
outputs:
  - name: new_function
    type: string
    description: The extracted function code
  - name: refactored_file
    type: string
    description: Updated source file with function extracted
  - name: call_sites
    type: array
    description: Locations where the extracted function is now called
tags:
  - refactoring
  - code-quality
  - maintainability
  - duplication
author: Neo Templates
last_updated: 2026-02-10
---

# Extract Function

## Purpose

Refactors code by extracting duplicate logic or complex code blocks into well-named, reusable functions. Improves code maintainability, testability, and reduces duplication.

## When to Use

- Code block is duplicated in multiple places
- Function or method is too long (>50 lines)
- Complex logic needs a descriptive name
- Code has deep nesting that could be flattened
- Logic should be testable in isolation
- Following the Single Responsibility Principle

## Prerequisites

- Source code file to refactor
- Understanding of the code's purpose and context
- Existing tests to verify behavior is preserved
- Ability to identify dependencies and side effects

## Inputs

### source_file
- **Type**: string
- **Required**: Yes
- **Description**: Path to the source file containing code to extract
- **Example**: `src/services/order-processor.js`

### target_code
- **Type**: string
- **Required**: Yes
- **Description**: The specific code block to extract, either as line range or code snippet
- **Example**: `lines 45-67` or the actual code block

### function_name
- **Type**: string
- **Required**: No (will suggest if not provided)
- **Description**: Name for the extracted function. Should be descriptive and follow naming conventions
- **Example**: `calculateOrderTotal`, `validateAddress`, `formatUserName`

## Outputs

### new_function
- **Type**: string
- **Description**: The newly created function with proper signature and body
- **Example**:
```javascript
function calculateOrderTotal(items, taxRate, discount) {
  const subtotal = items.reduce((sum, item) => sum + item.price * item.qty, 0);
  const tax = subtotal * taxRate;
  return subtotal + tax - discount;
}
```

### refactored_file
- **Type**: string
- **Description**: The updated source file with the extracted function in place
- **Example**: Shows the file with duplicated code replaced by function calls

### call_sites
- **Type**: array
- **Description**: List of locations where the extracted function is called
- **Example**:
```json
[
  {
    "file": "src/services/order-processor.js",
    "line": 45,
    "context": "processOrder function"
  },
  {
    "file": "src/services/order-processor.js",
    "line": 120,
    "context": "recalculateOrder function"
  }
]
```

## Implementation Steps

1. **Analyze target code**
   - Identify the exact code block to extract
   - Determine the code's purpose
   - Check for side effects or dependencies

2. **Identify inputs and outputs**
   - Find all variables read by the code (parameters)
   - Identify variables modified or created (return values)
   - Note any global state accessed

3. **Determine function signature**
   ```javascript
   // Inputs become parameters
   // Outputs become return value
   function extractedFunction(input1, input2, input3) {
     // logic here
     return output;
   }
   ```

4. **Choose appropriate function name**
   - Use descriptive, action-oriented names
   - Follow project naming conventions
   - Be specific about what the function does
   - Good: `calculateTotalWithTax`, `validateEmailFormat`
   - Bad: `doStuff`, `process`, `handle`

5. **Extract the function**
   - Create new function with identified parameters
   - Move code block into function body
   - Return appropriate value(s)
   - Handle edge cases and errors

6. **Replace original code with function call**
   - Replace extracted code with function call
   - Pass correct arguments
   - Use return value appropriately
   - Maintain same behavior

7. **Check for other occurrences**
   - Search for similar or duplicate code
   - Replace with calls to new function
   - Adjust parameters as needed for each call site

8. **Update tests**
   - Run existing tests to verify behavior preserved
   - Add specific tests for extracted function
   - Test edge cases and error conditions

9. **Optimize placement**
   - Place function in logical location (top of file, separate module)
   - Consider if function should be exported
   - Group with related functions

## Usage Examples

### Example 1: Extract duplicate validation logic
```markdown
Using skill: extract-function

Input:
- source_file: src/services/user-service.js
- target_code: Email validation logic (lines 34-42, 87-95, 156-164)
- function_name: validateEmailFormat

Before:
// In multiple places:
if (!email || !email.includes('@') || !email.includes('.')) {
  throw new Error('Invalid email format');
}
const domain = email.split('@')[1];
if (!domain || domain.length < 3) {
  throw new Error('Invalid email domain');
}

After:
function validateEmailFormat(email) {
  if (!email || !email.includes('@') || !email.includes('.')) {
    throw new Error('Invalid email format');
  }
  const domain = email.split('@')[1];
  if (!domain || domain.length < 3) {
    throw new Error('Invalid email domain');
  }
  return true;
}

// Replaced at 3 call sites:
validateEmailFormat(user.email);
```

### Example 2: Extract complex calculation
```markdown
Using skill: extract-function

Input:
- source_file: src/utils/pricing.js
- target_code: Lines 67-89 (discount calculation)
- function_name: calculateDiscountedPrice

Before:
function processOrder(order) {
  // ... 30 lines of code ...
  
  // Complex discount calculation
  let discount = 0;
  if (order.items.length > 5) {
    discount += order.total * 0.05;
  }
  if (order.customer.vip) {
    discount += order.total * 0.10;
  }
  if (order.total > 100) {
    discount += 10;
  }
  const maxDiscount = order.total * 0.25;
  discount = Math.min(discount, maxDiscount);
  
  const finalTotal = order.total - discount;
  // ... more code ...
}

After:
function calculateDiscountedPrice(total, itemCount, isVip) {
  let discount = 0;
  
  if (itemCount > 5) {
    discount += total * 0.05; // 5% bulk discount
  }
  if (isVip) {
    discount += total * 0.10; // 10% VIP discount
  }
  if (total > 100) {
    discount += 10; // $10 bonus discount
  }
  
  const maxDiscount = total * 0.25; // Cap at 25%
  discount = Math.min(discount, maxDiscount);
  
  return total - discount;
}

function processOrder(order) {
  // ... 30 lines of code ...
  
  const finalTotal = calculateDiscountedPrice(
    order.total,
    order.items.length,
    order.customer.vip
  );
  
  // ... more code ...
}
```

### Example 3: Extract nested logic
```markdown
Using skill: extract-function

Input:
- source_file: src/components/DataTable.jsx
- target_code: Lines 145-178 (nested sorting logic)
- function_name: sortTableData

Before:
function DataTable({ data }) {
  const handleSort = (column) => {
    const sorted = [...data].sort((a, b) => {
      if (column.type === 'number') {
        return a[column.key] - b[column.key];
      } else if (column.type === 'date') {
        return new Date(a[column.key]) - new Date(b[column.key]);
      } else {
        return a[column.key].localeCompare(b[column.key]);
      }
    });
    setData(sorted);
  };
  // ... rest of component
}

After:
function sortTableData(data, column) {
  return [...data].sort((a, b) => {
    const aVal = a[column.key];
    const bVal = b[column.key];
    
    if (column.type === 'number') {
      return aVal - bVal;
    }
    if (column.type === 'date') {
      return new Date(aVal) - new Date(bVal);
    }
    return aVal.localeCompare(bVal);
  });
}

function DataTable({ data }) {
  const handleSort = (column) => {
    const sorted = sortTableData(data, column);
    setData(sorted);
  };
  // ... rest of component
}

Benefits:
- sortTableData is now testable independently
- Sorting logic is reusable across components
- Main component is simpler and clearer
```

## Refactoring Patterns

### Extract Method
```javascript
// Before: Long method
function processUserRegistration(userData) {
  // validation code (15 lines)
  // password hashing code (8 lines)
  // database insertion code (12 lines)
  // email sending code (10 lines)
}

// After: Extracted methods
function processUserRegistration(userData) {
  validateUserData(userData);
  const hashedPassword = hashPassword(userData.password);
  const userId = saveUserToDatabase({ ...userData, password: hashedPassword });
  sendWelcomeEmail(userData.email, userId);
  return userId;
}
```

### Extract to Module
```javascript
// If function is used across files
// Before: Duplicated in multiple files
// After: Create shared module

// utils/validation.js
export function validateEmail(email) { /* ... */ }
export function validatePhone(phone) { /* ... */ }
export function validateAddress(address) { /* ... */ }

// user-service.js
import { validateEmail, validatePhone } from './utils/validation';
```

## Best Practices

- **Naming**: Use verbs for functions that perform actions
- **Size**: Extracted functions should be 5-20 lines ideally
- **Parameters**: Keep to 3-4 parameters max; use objects for more
- **Single Purpose**: Each function should do one thing well
- **Side Effects**: Minimize or clearly document side effects
- **Return Values**: Return meaningful values; avoid modifying parameters
- **Documentation**: Add JSDoc or docstrings explaining purpose

## Related Skills

- `code-analysis/detect-patterns` - Find duplication to extract
- `code-analysis/analyze-complexity` - Identify complex code to extract
- `refactoring/rename-symbol` - Improve extracted function names
- `testing/generate-unit-tests` - Test extracted functions

## Notes

- Always run tests before and after extraction
- Extract smallest meaningful unit first
- Consider extracting to separate file for reusability
- Use linter to catch issues after refactoring
- Commit extraction as separate change from other modifications
- Document why extraction was done in commit message
- Consider extracting to a class if functions share state
