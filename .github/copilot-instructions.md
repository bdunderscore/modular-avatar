# Copilot Instructions for Modular Avatar

## Project Overview

Modular Avatar is a suite of **non-destructive** tools for modularizing VRChat avatars and distributing avatar components. It allows users to easily add outfits, accessories, and gimmicks to avatars through drag-and-drop functionality.

### Key Technologies
- **Unity C#** - Core functionality for Unity Editor and runtime
- **VRChat SDK3** - Integration with VRChat avatar systems  
- **NDMF (Non-Destructive Modular Framework)** - Build system for non-destructive avatar modifications
- **UI Toolkit** - Modern Unity UI system for editor interfaces
- **Docusaurus 3.8.1** - Documentation website with internationalization
- **Localization** - Multi-language support for both code and documentation

### Repository Structure
- `Runtime/` - Core runtime components and systems
- `Editor/` - Unity Editor tools and inspectors
- `docs~/` - Docusaurus documentation site
  - `docs/` - English documentation
  - `i18n/ja/docusaurus-plugin-content-docs/current/` - Japanese documentation
- `UnitTests~/` - Unit tests for the project
- `Samples/` - Example prefabs and usage samples

## Code Style and Guidelines

### C# Coding Standards

#### General Principles
- Follow Microsoft C# coding conventions
- Use clear, descriptive names for variables, methods, and classes
- Prefer composition over inheritance
- Write self-documenting code with meaningful comments only when necessary
- Use nullable reference types where appropriate

#### Naming Conventions
```csharp
// Classes and interfaces: PascalCase
public class MergeArmature : AvatarTagComponent
public interface ILocalizationProvider

// Methods and properties: PascalCase  
public void ProcessAvatar()
public string DisplayName { get; set; }

// Private fields: camelCase with underscore prefix
private GameObject _targetObject;
private readonly Dictionary<string, float> _propertyOverrides;

// Parameters and local variables: camelCase
public void SetValue(string parameterName, float value)

// Constants: PascalCase
public const string DefaultPrefix = "MA_";
```

#### Unity-Specific Guidelines
- Always null-check Unity objects before use (`if (obj != null)`)
- Use `[SerializeField]` for private fields that need serialization
- Implement proper `OnValidate()` methods for editor-time validation
- Use `[HideInInspector]` judiciously to keep inspectors clean
- Leverage Unity's component lifecycle methods appropriately

#### Error Handling
- Use specific exception types when throwing exceptions
- Log errors using Unity's `Debug.LogError()` with context objects
- Validate inputs at public API boundaries
- Use `try-catch` blocks sparingly and only when you can handle the error meaningfully

#### UI Toolkit Guidelines
- Use USS (Unity Style Sheets) for styling instead of inline styles
- Implement proper localization callbacks for dynamic UI elements
- Follow the pattern established in `ROSimulator.cs` for UI element registration
- Use `Q<T>()` and `Q()` methods for element queries
- Register callbacks properly and clean them up when necessary

### Localization Guidelines

#### Code Localization
- Use `Localization.L.GetLocalizedString(key)` for user-facing strings
- Register language change callbacks for dynamic UI elements:
```csharp
LanguagePrefs.RegisterLanguageChangeCallback(element, relocalize);
```
- Store localization keys in appropriate `.po` files
- Always provide English fallbacks for localization keys

## Documentation Guidelines

### Style Guide for English Documentation

#### Writing Style
- **Tone**: Professional but approachable; assume users are familiar with Unity but may be new to Modular Avatar
- **Voice**: Active voice preferred; be direct and concise
- **Technical Level**: Provide both high-level explanations and detailed technical information
- **Formatting**: Use proper Markdown formatting with consistent heading levels

#### Structure Requirements
- All headings must include explicit named anchors: `## Section Name {#section-name}`
- Use kebab-case for anchor names (lowercase with hyphens)
- Start each page with appropriate frontmatter:
```yaml
---
sidebar_position: 1
sidebar_label: Display Name (if different from title)
---
```

#### Content Patterns
- Start with a brief description of what the component/feature does
- Include "When should I use it?" and "When shouldn't I use it?" sections for components
- Provide step-by-step setup instructions
- Include relevant screenshots with descriptive alt text
- Use Docusaurus admonitions (:::warning, :::tip, :::info) appropriately
- Link to related documentation using relative paths

#### Code Examples
- Use proper syntax highlighting for code blocks
- Provide complete, working examples where possible
- Include both C# code examples and Unity component usage

### Style Guide for Japanese Documentation

#### Writing Style (日本語ドキュメント)
- **敬語**: 基本的に丁寧語を使用（です・ます調）
- **技術用語**: 英語の技術用語はそのまま使用し、必要に応じて日本語で補足
- **読者層**: Unityに慣れているが、Modular Avatarは初心者という前提
- **文体**: 簡潔で分かりやすく、実用的な情報を重視

#### 構造要件
- 英語版と同じ見出し構造を維持し、同じ名前付きアンカーを使用
- フロントマターも英語版と同じ構造を保持
- 画像やリンクは英語版と同じものを参照

#### 翻訳のガイドライン
- 直訳ではなく、日本語として自然な表現を心がける
- 文化的な違いを考慮した説明を適宜追加
- VRChat日本語コミュニティでよく使われる用語を優先
- 英語版の更新に追従し、内容の同期を保つ

### Documentation Consistency Requirements

#### Language Synchronization
- Japanese documentation must mirror the English structure exactly
- Both versions must use identical named anchors for cross-linking consistency
- Update both language versions simultaneously when making documentation changes
- Ensure images and diagrams are accessible from both language versions

#### Cross-Language Linking
- Use named anchors to ensure stable URLs across languages
- Test links in both languages after updates
- Maintain parallel navigation structures in both `sidebars.js` and Japanese i18n

#### Image and Media Guidelines
- Store images in the English docs directory and reference from both languages
- Use descriptive filenames that indicate the content
- Provide appropriate alt text for accessibility
- Optimize images for web display while maintaining clarity

## Testing and Quality Assurance

### Unit Testing
- Write unit tests for all core business logic
- Use Unity Test Runner for Unity-specific tests
- Mock Unity objects appropriately in tests
- Maintain test coverage for critical paths

### Documentation Testing
- Build documentation locally before committing: `cd docs~ && yarn build`
- Test both English and Japanese versions
- Verify all links work in both languages
- Check that named anchors resolve correctly

### Integration Testing
- Test components in Unity with real avatar setups
- Verify non-destructive behavior (original assets unchanged)
- Test build processes with NDMF integration
- Validate VRChat SDK compatibility

## Build and Development Workflow

### Documentation Development
```bash
# Navigate to docs directory
cd docs~

# Install dependencies
yarn install

# Start development server
yarn start

# Build for production
yarn build

# Type checking
yarn typecheck
```

### Unity Development
- Use Unity 2022.3.22f1 (as specified in project)
- Test with VRChat SDK3 Avatars
- Verify NDMF integration works correctly
- Test both play mode and edit mode functionality

## Common Patterns and Anti-Patterns

### ✅ Good Practices
- Always check for null references before accessing Unity objects
- Use appropriate Unity lifecycle methods (`Awake`, `Start`, `OnEnable`)
- Implement proper cleanup in `OnDisable` and `OnDestroy`
- Use `[SerializeField]` for editor-exposed fields instead of public fields
- Validate component configuration in `OnValidate()`
- Use descriptive commit messages following conventional commits

### ❌ Anti-Patterns
- Don't modify objects that don't belong to the component
- Don't assume specific avatar structures without validation
- Avoid hardcoded strings for user-facing text (use localization)
- Don't create circular dependencies between components
- Avoid blocking the main thread with expensive operations
- Don't ignore compiler warnings

## Specific Project Considerations

### VRChat Integration
- Always consider VRChat's parameter and memory limits
- Test with both PC and Android (Quest) build targets
- Ensure compatibility with VRChat's avatar systems
- Follow VRChat's best practices for avatar optimization

### Non-Destructive Philosophy
- Never modify original prefab assets directly
- Always work on copies or generated objects
- Preserve user's original avatar configuration
- Allow users to disable/remove components cleanly

### Performance Considerations
- Minimize runtime overhead (most work should happen at build time)
- Cache expensive operations when possible
- Use object pooling for frequently created/destroyed objects
- Profile memory usage, especially for large avatars

## Getting Help
- Check existing GitHub issues before creating new ones
- Provide minimal reproduction cases for bugs
- Include Unity version, VRChat SDK version, and Modular Avatar version in reports
- Test issues with the latest version before reporting