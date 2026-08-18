---
name: generate-unit-tests
description: Generate comprehensive unit tests for functions, methods, and classes
version: 1.0.0
inputs:
  - name: target_file
    type: string
    description: Path to the source file to generate tests for
    required: true
  - name: test_framework
    type: string
    description: Testing framework to use (jest, mocha, pytest, junit, etc.)
    required: false
    default: auto-detect
  - name: coverage_goal
    type: number
    description: Target code coverage percentage
    required: false
    default: 80
outputs:
  - name: test_file_path
    type: string
    description: Path to the generated test file
  - name: test_cases
    type: array
    description: List of generated test cases with descriptions
tags:
  - testing
  - unit-tests
  - code-generation
  - quality-assurance
author: Neo Templates
last_updated: 2026-02-10
---

# Generate Unit Tests

## Purpose

Automatically generates comprehensive unit tests for source code, including happy paths, edge cases, error conditions, and boundary conditions. Ensures consistent test coverage and reduces manual test writing effort.

## When to Use

- After implementing new functions or classes
- When tests are missing for existing code
- During refactoring to ensure behavior preservation
- To establish baseline test coverage
- When standardizing test patterns across a codebase

## Prerequisites

- Source code file to test
- Testing framework installed (Jest, Mocha, pytest, JUnit, etc.)
- Understanding of the function/class responsibilities
- Mock/stub strategy for external dependencies

## Inputs

### target_file
- **Type**: string
- **Required**: Yes
- **Description**: Path to the source file containing code to test
- **Example**: `src/utils/date-formatter.js`

### test_framework
- **Type**: string
- **Required**: No
- **Default**: auto-detect (based on project configuration)
- **Description**: Testing framework to use for generating tests
- **Example**: `jest`, `mocha`, `pytest`, `junit`, `rspec`, `xunit`

### coverage_goal
- **Type**: number
- **Required**: No
- **Default**: 80
- **Description**: Target percentage of code coverage (0-100)
- **Example**: `90` for critical code, `70` for less critical

## Outputs

### test_file_path
- **Type**: string
- **Description**: Path where the test file was created
- **Example**: `src/utils/__tests__/date-formatter.test.js`

### test_cases
- **Type**: array
- **Description**: List of all generated test cases with descriptions
- **Example**:

```json
[
  {
    "function": "formatDate",
    "test_name": "should format date in ISO format",
    "type": "happy-path",
    "description": "Tests normal operation with valid date"
  },
  {
    "function": "formatDate",
    "test_name": "should handle null input gracefully",
    "type": "edge-case",
    "description": "Tests error handling for null input"
  }
]
```

## Implementation Steps

1. **Analyze the source code**
   - Parse the source file to identify all functions, methods, and classes
   - Extract function signatures, parameters, and return types
   - Identify dependencies and external calls
   - Analyze control flow and decision points

2. **Identify test scenarios**
   - **Happy path**: Normal operation with valid inputs
   - **Edge cases**: Empty strings, null, undefined, zero, empty arrays
   - **Boundary conditions**: Min/max values, limits
   - **Error conditions**: Invalid inputs, exceptions
   - **State transitions**: For classes with state
   - **Integration points**: External dependencies

3. **Determine mocking strategy**
   - Identify external dependencies (APIs, databases, file system)
   - Plan mocks/stubs for dependencies
   - Consider spy usage for verifying calls

4. **Generate test structure**
   - Create test file with appropriate naming convention
   - Set up test framework boilerplate
   - Import necessary testing utilities
   - Create describe/context blocks for organization

5. **Generate test cases**
   For each function/method:

   **a. Happy path tests**

   ```javascript
   test('should return formatted date string for valid input', () => {
     const result = formatDate('2026-02-10');
     expect(result).toBe('February 10, 2026');
   });
   ```

   **b. Edge case tests**

   ```javascript
   test('should handle null input', () => {
     expect(() => formatDate(null)).toThrow('Invalid date');
   });
   
   test('should handle empty string', () => {
     expect(() => formatDate('')).toThrow('Invalid date');
   });
   ```

   **c. Boundary tests**

   ```javascript
   test('should handle leap year date', () => {
     const result = formatDate('2024-02-29');
     expect(result).toBe('February 29, 2024');
   });
   ```

   **d. Error handling tests**

   ```javascript
   test('should throw error for invalid date format', () => {
     expect(() => formatDate('invalid')).toThrow();
   });
   ```

6. **Add setup and teardown**
   - Create beforeEach/afterEach hooks if needed
   - Initialize test data
   - Clean up after tests

7. **Add test utilities**
   - Helper functions for common test data
   - Custom matchers if needed
   - Mock factories

8. **Verify coverage**
   - Check that all branches are covered
   - Ensure all functions have tests
   - Verify coverage_goal is met

9. **Add documentation**
   - Comment complex test scenarios
   - Document test data meanings
   - Explain mock configurations

## Usage Examples

### Example 1: Generate tests for utility function
```markdown
Using skill: generate-unit-tests

Input:
- target_file: src/utils/string-helper.js
- test_framework: jest
- coverage_goal: 85

Generated: src/utils/__tests__/string-helper.test.js

Test cases created:
✓ capitalize()
  - should capitalize first letter of string
  - should handle empty string
  - should handle null/undefined
  - should preserve existing capitals

✓ truncate()
  - should truncate long strings
  - should add ellipsis when truncating
  - should not modify short strings
  - should handle custom max length

Coverage: 88% (exceeds goal of 85%)
```

### Example 2: Generate tests with mocking
```markdown
Using skill: generate-unit-tests

Input:
- target_file: src/services/user-service.js
- test_framework: jest
- coverage_goal: 80

Generated: src/services/__tests__/user-service.test.js

Test cases created:
✓ getUser()
  - should fetch user from API successfully
  - should return cached user if available
  - should handle API errors gracefully
  - should handle network timeout

Mocks created:
- API client mock for HTTP requests
- Cache service mock
- Logger mock

Note: Remember to configure mock implementations in test setup.
```

### Example 3: Generate tests for class
```markdown
Using skill: generate-unit-tests

Input:
- target_file: src/models/ShoppingCart.js
- test_framework: jest
- coverage_goal: 90

Generated: src/models/__tests__/ShoppingCart.test.js

Test cases created:
✓ Constructor
  - should initialize empty cart
  - should load existing items if provided

✓ addItem()
  - should add new item to cart
  - should increment quantity for existing item
  - should validate item object
  - should emit change event

✓ removeItem()
  - should remove item from cart
  - should handle non-existent item
  - should emit change event

✓ getTotal()
  - should calculate total correctly
  - should handle empty cart
  - should apply discounts if present

Coverage: 92% (exceeds goal of 90%)
```

## Test Patterns

### Arrange-Act-Assert (AAA)
```javascript
test('should calculate discount correctly', () => {
  // Arrange
  const cart = new ShoppingCart();
  cart.addItem({ price: 100 });
  
  // Act
  const discount = cart.applyDiscount(0.1);
  
  // Assert
  expect(discount).toBe(10);
  expect(cart.getTotal()).toBe(90);
});
```

### Given-When-Then (BDD)
```javascript
describe('ShoppingCart', () => {
  describe('when applying a discount', () => {
    it('should reduce the total price', () => {
      // Given a cart with items
      const cart = new ShoppingCart();
      cart.addItem({ price: 100 });
      
      // When a discount is applied
      cart.applyDiscount(0.1);
      
      // Then the total should be reduced
      expect(cart.getTotal()).toBe(90);
    });
  });
});
```

## Related Skills

- `testing/generate-integration-tests` - Create integration tests
- `testing/validate-test-coverage` - Verify coverage meets goals
- `code-analysis/analyze-complexity` - Complex code needs more tests
- `refactoring/extract-function` - Simplify code before testing

## Notes

- Generated tests are a starting point; review and refine them
- Add assertions that verify business logic, not just syntax
- Mock external dependencies to keep tests fast and isolated
- Consider property-based testing for complex logic
- Update tests when refactoring code
- Use descriptive test names that explain what is being tested
