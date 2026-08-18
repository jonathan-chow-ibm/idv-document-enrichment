---
name: generate-jsdoc
description: Generate JSDoc, docstring, or inline documentation for functions and classes
version: 1.0.0
inputs:
  - name: source_file
    type: string
    description: Path to the source file to document
    required: true
  - name: doc_style
    type: string
    description: Documentation style (jsdoc, python-docstring, javadoc, xmldoc)
    required: false
    default: auto-detect
  - name: completeness_level
    type: string
    description: Level of documentation detail (minimal, standard, comprehensive)
    required: false
    default: standard
outputs:
  - name: documented_file
    type: string
    description: Updated source file with documentation added
  - name: documentation_added
    type: array
    description: List of functions/classes that received documentation
tags:
  - documentation
  - code-quality
  - maintainability
  - api-docs
author: Neo Templates
last_updated: 2026-02-10
---

# Generate JSDoc

## Purpose

Automatically generates comprehensive documentation comments (JSDoc, docstrings, etc.) for functions, methods, classes, and modules. Improves code maintainability and enables automatic API documentation generation.

## When to Use

- Adding documentation to undocumented code
- Standardizing documentation across a codebase
- Preparing code for API documentation generation
- During code review when documentation is missing
- After refactoring to update documentation
- When establishing documentation standards

## Prerequisites

- Source code file to document
- Understanding of function/class purposes
- Knowledge of documentation style guidelines
- Type information (if using TypeScript or type hints)

## Inputs

### source_file
- **Type**: string
- **Required**: Yes
- **Description**: Path to source file that needs documentation
- **Example**: `src/services/auth-service.js`

### doc_style
- **Type**: string
- **Required**: No
- **Default**: auto-detect (based on file extension)
- **Description**: Documentation format to use
- **Options**:
  - `jsdoc` - JavaScript/TypeScript JSDoc format
  - `python-docstring` - Python docstrings (Google, NumPy, or Sphinx style)
  - `javadoc` - Java Javadoc format
  - `xmldoc` - C# XML documentation
  - `rdoc` - Ruby RDoc format
- **Example**: `jsdoc`

### completeness_level
- **Type**: string
- **Required**: No
- **Default**: `standard`
- **Description**: How detailed the documentation should be
- **Options**:
  - `minimal` - Just description and parameters
  - `standard` - Description, parameters, returns, throws
  - `comprehensive` - Standard + examples, see-also, notes
- **Example**: `comprehensive`

## Outputs

### documented_file
- **Type**: string
- **Description**: Path to the file with documentation added
- **Example**: `src/services/auth-service.js` (updated)

### documentation_added
- **Type**: array
- **Description**: List of documented items with summary
- **Example**:
```json
[
  {
    "name": "authenticateUser",
    "type": "function",
    "line": 15,
    "doc_type": "jsdoc",
    "completeness": "standard"
  },
  {
    "name": "AuthService",
    "type": "class",
    "line": 45,
    "doc_type": "jsdoc",
    "completeness": "comprehensive"
  }
]
```

## Implementation Steps

1. **Parse source file**
   - Read and analyze the source code
   - Identify all functions, methods, and classes
   - Extract existing documentation (if any)
   - Determine language and convention

2. **Analyze code elements**
   For each undocumented item:
   - Extract function/method signature
   - Identify parameters and types
   - Determine return type
   - Find thrown exceptions/errors
   - Analyze usage context

3. **Generate documentation structure**
   Based on doc_style:
   
   **JSDoc (JavaScript/TypeScript)**
   ```javascript
   /**
    * Description
    * @param {Type} paramName - Parameter description
    * @returns {Type} Return value description
    * @throws {ErrorType} Error condition description
    * @example
    * // Usage example
    */
   ```
   
   **Python Docstring (Google style)**
   ```python
   """Description.
   
   Args:
       param_name (Type): Parameter description
       
   Returns:
       Type: Return value description
       
   Raises:
       ErrorType: Error condition description
       
   Example:
       >>> function_call()
   """
   ```

4. **Write descriptions**
   - **Function description**: What it does (not how)
   - Be concise but informative
   - Use imperative mood ("Calculate total" not "Calculates total")
   - Describe purpose and behavior
   - Note any side effects

5. **Document parameters**
   - List each parameter with type
   - Describe what it represents
   - Note if optional and default value
   - Mention valid ranges or constraints
   - Indicate if parameter is modified

6. **Document return values**
   - Specify return type
   - Describe what is returned
   - Note special return values (null, undefined, empty)
   - Document return value structure for objects

7. **Document exceptions/errors**
   - List all thrown exceptions
   - Describe conditions that trigger each
   - Provide recovery guidance if applicable

8. **Add examples (for comprehensive level)**
   - Show typical usage
   - Include edge cases if relevant
   - Use realistic data
   - Keep examples concise

9. **Add additional sections (for comprehensive level)**
   - `@see` - Related functions
   - `@since` - Version added
   - `@deprecated` - If function is deprecated
   - `@note` - Important implementation notes
   - `@todo` - Future improvements

10. **Format and insert documentation**
    - Apply proper indentation
    - Follow style guide conventions
    - Insert above function/class definition
    - Preserve existing comments

## Usage Examples

### Example 1: Generate JSDoc for JavaScript function
```markdown
Using skill: generate-jsdoc

Input:
- source_file: src/utils/calculator.js
- doc_style: jsdoc
- completeness_level: standard

Before:
function calculateDiscount(price, discountPercent, maxDiscount) {
  const discount = price * (discountPercent / 100);
  return Math.min(discount, maxDiscount);
}

After:
/**
 * Calculates the discount amount for a given price with a maximum cap.
 * 
 * @param {number} price - The original price before discount
 * @param {number} discountPercent - The discount percentage (0-100)
 * @param {number} maxDiscount - The maximum discount amount allowed
 * @returns {number} The calculated discount amount, capped at maxDiscount
 * @throws {Error} If price or maxDiscount is negative
 */
function calculateDiscount(price, discountPercent, maxDiscount) {
  if (price < 0 || maxDiscount < 0) {
    throw new Error('Price and maxDiscount must be non-negative');
  }
  const discount = price * (discountPercent / 100);
  return Math.min(discount, maxDiscount);
}
```

### Example 2: Comprehensive documentation with examples
```markdown
Using skill: generate-jsdoc

Input:
- source_file: src/services/user-service.js
- doc_style: jsdoc
- completeness_level: comprehensive

After:
/**
 * Authenticates a user with email and password credentials.
 * 
 * This method validates credentials against the database, checks if the
 * account is active, and generates a JWT token for authenticated sessions.
 * 
 * @param {string} email - User's email address
 * @param {string} password - User's password (will be hashed for comparison)
 * @param {Object} [options={}] - Additional authentication options
 * @param {boolean} [options.rememberMe=false] - Whether to extend token lifetime
 * @param {string} [options.ipAddress] - Client IP for security logging
 * @returns {Promise<Object>} Authentication result
 * @returns {string} returns.token - JWT authentication token
 * @returns {Object} returns.user - Authenticated user object
 * @returns {string} returns.user.id - User ID
 * @returns {string} returns.user.email - User email
 * @returns {string} returns.user.name - User display name
 * @throws {AuthenticationError} If credentials are invalid
 * @throws {AccountLockedError} If account is locked due to failed attempts
 * @throws {AccountInactiveError} If account is not active
 * 
 * @example
 * // Basic authentication
 * const result = await authenticateUser('user@example.com', 'password123');
 * console.log(result.token); // "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
 * 
 * @example
 * // With remember me option
 * const result = await authenticateUser(
 *   'user@example.com',
 *   'password123',
 *   { rememberMe: true }
 * );
 * 
 * @see {@link validatePassword} for password validation rules
 * @see {@link generateToken} for token generation details
 * @since 1.0.0
 */
async function authenticateUser(email, password, options = {}) {
  // Implementation...
}
```

### Example 3: Python docstring generation
```markdown
Using skill: generate-jsdoc

Input:
- source_file: src/models/user.py
- doc_style: python-docstring
- completeness_level: standard

Before:
def create_user(username, email, password, role='user'):
    user = User(username=username, email=email)
    user.set_password(password)
    user.role = role
    db.session.add(user)
    db.session.commit()
    return user

After:
def create_user(username, email, password, role='user'):
    """Create a new user account with the specified credentials.
    
    Args:
        username (str): Unique username for the account
        email (str): User's email address (must be valid format)
        password (str): Plain text password (will be hashed before storage)
        role (str, optional): User role. Defaults to 'user'.
            Valid values: 'user', 'admin', 'moderator'
    
    Returns:
        User: The newly created user object with generated ID
    
    Raises:
        ValidationError: If username or email is invalid
        DuplicateUserError: If username or email already exists
        
    Example:
        >>> user = create_user('john_doe', 'john@example.com', 'secure123')
        >>> user.id
        42
    """
    user = User(username=username, email=email)
    user.set_password(password)
    user.role = role
    db.session.add(user)
    db.session.commit()
    return user
```

### Example 4: Class documentation
```markdown
Using skill: generate-jsdoc

Input:
- source_file: src/models/ShoppingCart.js
- doc_style: jsdoc
- completeness_level: comprehensive

After:
/**
 * Represents a shopping cart for e-commerce transactions.
 * 
 * The ShoppingCart manages items, calculates totals, applies discounts,
 * and maintains cart state throughout the shopping session. It supports
 * persistence to local storage and synchronization with the backend.
 * 
 * @class
 * @classdesc Manages shopping cart operations including items, totals, and checkout
 * 
 * @example
 * // Create a new cart
 * const cart = new ShoppingCart();
 * 
 * @example
 * // Add items to cart
 * cart.addItem({ id: '123', name: 'Widget', price: 29.99, qty: 2 });
 * console.log(cart.getTotal()); // 59.98
 * 
 * @property {Array<CartItem>} items - Array of items in the cart
 * @property {number} totalItems - Total number of items (sum of quantities)
 * @property {string} userId - ID of the user who owns this cart
 * @property {Date} createdAt - Timestamp when cart was created
 * @property {Date} updatedAt - Timestamp of last cart modification
 * 
 * @since 1.0.0
 */
class ShoppingCart {
  /**
   * Creates a new ShoppingCart instance.
   * 
   * @param {string} [userId=null] - Optional user ID for cart persistence
   * @param {Array<CartItem>} [existingItems=[]] - Pre-populate cart with items
   */
  constructor(userId = null, existingItems = []) {
    // Implementation...
  }
  
  /**
   * Adds an item to the cart or updates quantity if item exists.
   * 
   * @param {CartItem} item - Item to add to cart
   * @param {string} item.id - Unique product ID
   * @param {string} item.name - Product name
   * @param {number} item.price - Unit price
   * @param {number} [item.qty=1] - Quantity to add
   * @returns {CartItem} The added or updated cart item
   * @throws {InvalidItemError} If item data is invalid
   * @fires ShoppingCart#itemAdded
   */
  addItem(item) {
    // Implementation...
  }
}
```

## Documentation Style Guidelines

### Good Documentation
- ✅ Clear, concise descriptions
- ✅ Accurate parameter types
- ✅ Complete parameter list
- ✅ Return value described
- ✅ Error conditions documented
- ✅ Realistic examples included

### Poor Documentation
- ❌ Vague descriptions ("Does stuff")
- ❌ Missing parameter descriptions
- ❌ No type information
- ❌ Ignoring error conditions
- ❌ No examples for complex functions

## Related Skills

- `documentation/update-readme` - Update project README files
- `documentation/create-api-docs` - Generate full API documentation
- `code-analysis/detect-patterns` - Identify undocumented patterns
- `refactoring/extract-function` - Document newly extracted functions

## Notes

- Generated documentation is a starting point; review and refine it
- Keep documentation in sync with code changes
- Use tools like JSDoc, Sphinx, or Doxygen to generate HTML docs
- Consider API documentation standards (OpenAPI, etc.)
- Document complex algorithms with additional notes
- Include @deprecated tags for legacy functions
- Use consistent terminology across documentation
- Link related functions with @see tags
