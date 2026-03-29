# OnFlight Style Guide

> **⚠️ IMPORTANT: When modifying any style (colors, icons, layout tokens, converters, etc.), please update this document accordingly to keep it in sync with the codebase.**

> Style reference for the OnFlight Avalonia UI project.
> Search tag prefix: `[Style:XXX]` — use it to locate code that references a specific token.

---

## 0. Base Theme `[Style:BaseTheme]`

The project uses **`Avalonia.Themes.Simple`** (`SimpleTheme`) as the baseline theme. All custom visual styles are layered on top via `Theme.axaml`.

| Item | Value |
|------|-------|
| NuGet Package | `Avalonia.Themes.Simple` 11.* |
| App.axaml declaration | `<SimpleTheme />` |
| Custom styles overlay | `Styles/Theme.axaml` |

### 0.1 SimpleTheme Template Part Compatibility

SimpleTheme uses the same `PART_*` naming convention as FluentTheme for most controls. The selectors in `Theme.axaml` target these template parts:

| Selector | Exists in SimpleTheme | Notes |
|---|---|---|
| `ContentPresenter#PART_ContentPresenter` | ✅ | Button, TabItem — identical to Fluent |
| `Border#PART_BorderElement` | ✅ | TextBox — identical to Fluent |
| `Border#PART_Indicator` | ✅ | ProgressBar — identical to Fluent |
| `Border#border` | ✅ | ComboBox outer border (lowercase, not a PART) |
| `Border#PART_SelectedPipe` | ❌ | TabItem — does **not** exist in SimpleTheme (existed in FluentTheme) |

### 0.2 SimpleTheme Button Differences

SimpleTheme's Button `ContentPresenter#PART_ContentPresenter` renders `Background` and `BorderBrush` directly (no wrapping Border). On `:pointerover`, SimpleTheme applies a visible border highlight by default. To prevent a white flash on hover, all button style classes (`IconBtn`, `PlainIconBtn`, `CheckBtn`, `Fab`, `Breadcrumb`, `WinBtn`) set `Background` and `BorderThickness` on `PART_ContentPresenter` in the **default state** (not just hover/pressed), plus include `:pointerover` and `:pressed` template overrides:

```xml
<!-- Default state — prevents SimpleTheme flash -->
<Style Selector="Button.MyClass /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="Background" Value="..."/>
    <Setter Property="BorderThickness" Value="0"/>
</Style>
<Style Selector="Button.MyClass:pointerover /template/ ContentPresenter#PART_ContentPresenter">
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Background" Value="..."/>
</Style>
```

### 0.3 Acrylic / Mica with SimpleTheme

SimpleTheme's `Window` ControlTemplate contains a `Border Background="{TemplateBinding Background}"` layer beneath the content. If the Window's `Background` is opaque, this Border blocks `ExperimentalAcrylicBorder` with `BackgroundSource="Digger"` from sampling underlying content.

**Required pattern for windows with acrylic:**

1. Set `Background="Transparent"` on the `<Window>` element
2. Add an opaque background `Border` as the **first child** inside the root Grid:

```xml
<Window Background="Transparent" TransparencyLevelHint="AcrylicBlur" ...>
    <Grid>
        <!-- Opaque background layer (beneath acrylic, spanning full grid) -->
        <Border Grid.Row="0" Grid.RowSpan="..." Grid.Column="0" Grid.ColumnSpan="..."
                Background="{DynamicResource AppBackground}" IsHitTestVisible="False"/>
        <!-- ... rest of content with ExperimentalAcrylicBorder ... -->
    </Grid>
</Window>
```

This ensures the template-level Border is transparent (allowing acrylic composition), while the content-level Border provides the opaque fallback color.

**Windows using this pattern:** `MainWindow`, `SettingsWindow`
**Windows NOT needing this:** `FloatingWindow`, `ConfirmDialog` (already `Background="Transparent"`)

### 0.4 Foreground Color Inheritance Rule

SimpleTheme defines its own `ThemeForegroundBrush` which does **not** track our `PrimaryText` token. Any container control whose children rely on inherited `Foreground` (e.g. `TabControl`, `ContentControl`) must explicitly set `Foreground="{DynamicResource PrimaryText}"` to ensure text is correct in both light and dark themes.

**Currently applied to:** `TabControl.PanelTabs`

---

## 1. Design Token Colors

All color tokens are defined in `Theme.axaml` inside `ThemeDictionaries`.

### 1.1 Light Theme `[Style:LightTheme]`

| Token Key               | Color     | Usage                            |
|--------------------------|-----------|----------------------------------|
| `AppBackground`          | `#F2F2F7` | Window root background           |
| `CardBackground`         | `#FFFFFF` | Card / panel background          |
| `SidebarBackground`      | `#E8E8ED` | Sidebar area background          |
| `ContentBackground`      | `#FFFFFF` | Main content area background     |
| `SeparatorBrush`         | `#E5E5EA` | Dividers, progress bar track     |
| `PrimaryText`            | `#000000` | Primary text color               |
| `SecondaryText`          | `#8E8E93` | Secondary / hint text            |
| `TertiaryText`           | `#C7C7CC` | Tertiary / placeholder text      |
| `HoverBackground`        | `#1A007AFF` | Hover state (10% AccentBlue)     |
| `SelectedBackground`     | `#33007AFF` | Selected item (20% AccentBlue)   |
| `ArchiveBannerBg`        | `#E8F5E9` | Archive countdown banner bg      |
| `ForkChildBg`            | `#F9F9FB` | Fork-child row tint              |
| `InputBorderBrush`       | `#D1D1D6` | TextBox / ComboBox border        |
| `InputFocusBorderBrush`  | `#007AFF` | TextBox focused border           |
| `InputBackground`        | `#FFFFFF` | TextBox / input background       |
| `IconForeground`         | `#3C3C43` | General icon tint                |
| `RightPanelBackground`   | `#F9F9FB` | Right panel (Col 4) background   |
| `TitleBarActionBg`       | `#E5E5EA` | Title bar action button bg       |
| `TitleBarActionFg`       | `#333333` | Title bar action button fg/icon  |

### 1.2 Dark Theme `[Style:DarkTheme]`

| Token Key               | Color     | Usage                            |
|--------------------------|-----------|----------------------------------|
| `AppBackground`          | `#1C1C1E` | Window root background           |
| `CardBackground`         | `#2C2C2E` | Card / panel background          |
| `SidebarBackground`      | `#161618` | Sidebar area background          |
| `ContentBackground`      | `#2C2C2E` | Main content area background     |
| `SeparatorBrush`         | `#38383A` | Dividers, progress bar track     |
| `PrimaryText`            | `#FFFFFF` | Primary text color               |
| `SecondaryText`          | `#8E8E93` | Secondary / hint text            |
| `TertiaryText`           | `#48484A` | Tertiary / placeholder text      |
| `HoverBackground`        | `#1A0A84FF` | Hover state (10% AccentBlue)     |
| `SelectedBackground`     | `#330A84FF` | Selected item (20% AccentBlue)   |
| `ArchiveBannerBg`        | `#1B3A1B` | Archive countdown banner bg      |
| `ForkChildBg`            | `#2A2A2C` | Fork-child row tint              |
| `InputBorderBrush`       | `#48484A` | TextBox / ComboBox border        |
| `InputFocusBorderBrush`  | `#0A84FF` | TextBox focused border           |
| `InputBackground`        | `#2C2C2E` | TextBox / input background       |
| `IconForeground`         | `#EBEBF5` | General icon tint                |
| `RightPanelBackground`   | `#2A2A2C` | Right panel (Col 4) background   |
| `TitleBarActionBg`       | `#3A3A3C` | Title bar action button bg       |
| `TitleBarActionFg`       | `#EBEBF5` | Title bar action button fg/icon  |

### 1.3 Shared Accent Colors `[Style:AccentColors]`

| Token Key            | Color     | Semantic Use                          |
|-----------------------|-----------|---------------------------------------|
| `AccentBlueBrush`     | `#007AFF` | Primary action, links, icon buttons   |
| `AccentGreenBrush`    | `#34C759` | Progress bar, "Done" status, archive  |
| `AccentOrangeBrush`   | `#FF9500` | Draft badge, "Pending" status         |
| `AccentRedBrush`      | `#FF3B30` | Close button hover, destructive       |
| `AccentPurpleBrush`   | `#AF52DE` | History icon badge                    |
| `AccentTealBrush`     | `#5AC8FA` | Breadcrumb text                       |
| `AccentIndigoBrush`   | `#5856D6` | (Reserved)                            |

### 1.4 Layout Tokens `[Style:LayoutTokens]`

| Token Key              | Value | Usage                             |
|-------------------------|-------|-----------------------------------|
| `CardCornerRadius`      | `12`  | Card / ListItem border radius     |
| `ButtonCornerRadius`    | `8`   | FAB / general button radius       |
| `InputCornerRadius`     | `10`  | TextBox / ComboBox radius         |
| `FloatingCornerRadius`  | `12`  | FloatingWindow outer corner       |

---

## 2. Style Classes (Theme.axaml)

### 2.1 `Border.Card` `[Style:Card]`

- **Background**: `CardBackground`
- **CornerRadius**: `CardCornerRadius (12)`
- **Padding**: `16`, **Margin**: `4`
- **BoxShadow**: `0 1 3 0 #1A000000`
- **Transitions**: Opacity 0.3s + translateY 0.3s CubicEaseOut

### 2.2 `TextBlock.SectionHeader` `[Style:SectionHeader]`

- **FontSize**: `24`, **FontWeight**: `SemiBold`
- **Margin**: `0,0,0,12`
- **Foreground**: `PrimaryText`

### 2.3 `Button.Breadcrumb` `[Style:Breadcrumb]`

- **Background**: Transparent, **BorderThickness**: 0
- **Padding**: `8,4`, **FontSize**: `13`
- **Foreground**: `AccentTealBrush (#5AC8FA)`

### 2.4 `ProgressBar.Apple` `[Style:ProgressBar]`

- **Height**: `6`, **MinHeight**: `6`, **CornerRadius**: `3`
- **Foreground (fill)**: `AccentGreenBrush (#34C759)`
- **Background (track)**: `SeparatorBrush`
- **BorderThickness**: `0`
- **Template**: `Border#PART_Indicator` also gets `CornerRadius: 3` for rounded fill

### 2.5 `Border.ListItem` `[Style:ListItem]`

- **Background**: Transparent → `HoverBackground` on `:pointerover`
- **CornerRadius**: `CardCornerRadius (12)`
- **Padding**: `10,8`, **Margin**: `2,1`
- **Transitions**: Background 0.1s, RenderTransform 0.2s, Opacity 0.15s

### 2.6 `Border.FloatingBall` `[Style:FloatingBall]`

- **Size**: `56×56`, **CornerRadius**: `28` (circle)
- **Background**: `AccentBlueBrush (#007AFF)`
- **BoxShadow**: `0 4 12 0 #33000000`

### 2.7 `Button.IconBtn` `[Style:IconBtn]`

- **Size**: `26×26`, **CornerRadius**: `13` (circle)
- **Background**: `AccentBlueBrush (#007AFF)`, **Foreground**: `White`
- **Opacity**: `0.85` → `1.0` on `:pointerover`

### 2.8 `Button.PlainIconBtn` `[Style:PlainIconBtn]`

- **Background**: Transparent → `HoverBackground` on `:pointerover`
- **CornerRadius**: `6`

### 2.9 `Button.CheckBtn` `[Style:CheckBtn]`

- **Background**: Transparent, **CornerRadius**: `12`
- **Pressed state**: bg `#BBBBBB`, foreground `#BBBBBB`

### 2.10 `Button.Fab` `[Style:Fab]`

- **Background**: `AccentBlueBrush (#007AFF)`, **Foreground**: `White`
- **CornerRadius**: `ButtonCornerRadius (8)`
- **Padding**: `12,8`

### 2.11 `TabControl.PanelTabs` / `TabItem` `[Style:PanelTab]`

- **TabControl.PanelTabs Foreground**: `PrimaryText` — ensures tab **content area** text inherits the correct color (SimpleTheme's default `ThemeForegroundBrush` does not follow our token system)
- **FontSize**: `15`, **FontWeight**: `SemiBold`
- **CornerRadius**: `InputCornerRadius (10)`
- **Padding**: `14,6`, **Margin**: `0,0,6,0`
- **Default**: `Background` Transparent, `Foreground` `SecondaryText`
- **Hover**: `Background` → `HoverBackground`, `Foreground` → `PrimaryText`
- **Selected**: `Background` → `AccentBlueBrush` (solid), `Foreground` → `White`
- **Transitions**: Background 0.15s, Foreground 0.15s
- **Note**: `PART_SelectedPipe` does not exist in SimpleTheme — no hide-override needed (was required under FluentTheme)

### 2.12 `TextBox` (global) `[Style:TextBox]`

- **BorderBrush**: `InputBorderBrush`, **BorderThickness**: `1`
- **Background**: `InputBackground`, **CornerRadius**: `InputCornerRadius (10)`
- **Padding**: `10,8`
- **Focus**: border → `InputFocusBorderBrush`, thickness `1.5`
- **Hover**: border → `InputFocusBorderBrush`

### 2.13 `TextBox.InlineRename` `[Style:InlineRename]`

- **Border**: bottom-only `0,0,0,1` transparent → `AccentBlueBrush` on focus (`0,0,0,1.5`)
- **Background**: Transparent, **CornerRadius**: `0`

### 2.14 `CheckBox` (global) `[Style:CheckBox]`

Unified with `Border.ListItem` appearance:

- **Background**: Transparent → `HoverBackground` on `:pointerover`
- **CornerRadius**: `CardCornerRadius (12)`
- **Padding**: `10,8`, **Margin**: `2,1`
- **Foreground**: `PrimaryText`
- **Transitions**: Background 0.1s

### 2.15 `ComboBox` (global) `[Style:ComboBox]`

- **Border/bg**: same as TextBox pattern
- **CornerRadius**: `CardCornerRadius (12)` (unified with ListItem)
- **Padding**: `10,6`
- **Hover**: `Border#border` borderBrush → `InputFocusBorderBrush`
- **Focus**: `Border#border` borderBrush → `InputFocusBorderBrush`, thickness `1.5`
- **Popup dropdown**: Background `CardBackground`, BorderBrush `SeparatorBrush`, BorderThickness `1`, CornerRadius `CardCornerRadius (12)`, Padding `4`
- **ComboBoxItem**: Foreground `PrimaryText`, Padding `10,8`, Margin `2,1`, CornerRadius `CardCornerRadius (12)`
- **ComboBoxItem hover**: Background → `HoverBackground`, rounded corners
- **ComboBoxItem selected**: Background → `SelectedBackground`, rounded corners

### 2.16 `Button.WinBtn` / `Button.WinClose` `[Style:WinBtn]` `[Style:WinClose]`

- **Size**: `24×24`, **CornerRadius**: `6`
- **Icon size**: `11×11`
- **Foreground**: `SecondaryText` → `PrimaryText` on hover
- **WinClose hover**: bg → `AccentRedBrush (#FF3B30)`, fg → `White`

### 2.17 `ListBox` / `ListBoxItem` (global) `[Style:ListBox]`

Global styles defined in `Theme.axaml` to override SimpleTheme defaults:

- **ListBox**: Background `Transparent`, BorderThickness `0`
- **ListBoxItem**: Foreground `PrimaryText`, Background `Transparent`
- **Padding**: `10,8`, **Margin**: `0,1`, **CornerRadius**: `8`
- **Hover**: Background → `HoverBackground` (via `/template/ ContentPresenter`)
- **Selected**: Background → `SelectedBackground` (stable across `:pointerover` and `:focus`)

Sidebar ListBoxes in `MainWindow.axaml` and `SettingsWindow.axaml` add inline overrides for `FontSize: 13`, `HorizontalContentAlignment: Stretch`.

### 2.18 `ScrollBar` (global) `[Style:ScrollBar]`

Fully custom-templated thin rounded scrollbar to match Apple aesthetic:

- **Background**: Transparent (no track)
- **Vertical**: `Width: 6`, custom `ControlTemplate` — outer `Border` with `CornerRadius: 3`, `ClipToBounds`, containing only a `Track` + `Thumb`
- **Horizontal**: `Height: 6`, same custom template (orientation flipped)
- **Thumb**: custom `ControlTemplate` — `Border` with `Background: TertiaryText`, `CornerRadius: 3` (rounded pill)
- **Vertical Thumb**: `MinHeight: 20`; **Horizontal Thumb**: `MinWidth: 20`
- **Opacity**: `0.5` → `0.8` on `:pointerover`
- No RepeatButtons (arrows removed from template)
- **Transition**: Opacity 0.15s

### 2.19 `FlyoutPresenter` `[Style:Flyout]`

- **Background**: `CardBackground`
- **BorderBrush**: `SeparatorBrush`, **BorderThickness**: `1`
- **CornerRadius**: `FloatingCornerRadius (12)`
- **Padding**: `12`

### 2.20 `ToolTip` `[Style:ToolTip]`

- **Background**: `CardBackground`, **Foreground**: `PrimaryText`
- **BorderBrush**: `SeparatorBrush`, **BorderThickness**: `1`
- **CornerRadius**: `6`
- **Padding**: `8,4`, **FontSize**: `12`

### 2.21 `ConfirmDialog` `[Style:ConfirmDialog]`

- **Window**: `Background="Transparent"`, `TransparencyLevelHint="Transparent"`, `SystemDecorations="None"`
- **Outer Border**: `CardBackground`, `CardCornerRadius (12)`, `Padding="24,20"`, `Margin="8"`
- **BoxShadow**: `0 4 16 0 #28000000`
- **Title**: FontSize `17`, FontWeight `SemiBold`, `PrimaryText`
- **Message**: FontSize `13`, `SecondaryText`, `LineHeight="20"`
- **Buttons** (code-behind): `Padding="16,6"`, FontSize `13`
  - Destructive: `Fab` class + `#FF3B30` bg + White fg
  - Primary: `Fab` class
  - Secondary: `PlainIconBtn` class + `CornerRadius=8`

---

## 3. Converters `[Style:Converters]`

All converters are registered in `App.axaml` and defined in `Converters/`.

| Key (x:Key)            | Class                          | Input → Output                                                    | Details                                       |
|-------------------------|--------------------------------|-------------------------------------------------------------------|-----------------------------------------------|
| `DoneToIcon`            | `DoneToIconKindConverter`      | `bool` → `MaterialIconKind`                                      | `true` → CheckCircleOutline, `false` → CircleOutline |
| `DoneToBrush`           | `DoneToBrushConverter`         | `bool` → `SolidColorBrush`                                       | Theme-aware: Light `#D2D2D2`/`#C8C8C8`, Dark `#48484A`/`#636366` |
| `DoneToTextBrush`       | `DoneToTextBrushConverter`     | `bool` → `SolidColorBrush`                                       | Theme-aware: Light `#D2D2D2`/`#000000`, Dark `#48484A`/`#FFFFFF` |
| `NodeTypeToIcon`        | `NodeTypeToIconConverter`      | `FlowNodeType` → `MaterialIconKind`                               | Task→CheckCircle, Loop→Replay, Fork→CallSplit, Join→CallMerge, Branch→SourceBranch |
| `NodeTypeToRotation`    | `NodeTypeToRotationConverter`  | `FlowNodeType` → `double`                                        | Fork/Join → `180.0` (rotated for vertical flow), others → `0.0` |
| `StatusToBrush`         | `StatusToBrushConverter`       | `TodoStatus` → `SolidColorBrush`                                 | Done→`#34C759`, Ready→`#007AFF`, Skipped→`#8E8E93`, Pending→`#FF9500` |
| `StatusToStrikethrough` | `StatusToStrikethroughConverter`| `TodoStatus` → `TextDecorationCollection?`                        | Done → Strikethrough, else → null              |
| `DepthToMargin`         | `DepthToMarginConverter`       | `int` → `Thickness`                                              | `depth * 24` left margin                       |
| `HexToBrush`            | `HexToBrushConverter`          | `string (hex)` → `SolidColorBrush`                               | Parses hex, fallback → `Orange`                |
| `BoolToVis`             | `BoolToVisibilityConverter`    | `bool/int/string` → `bool` (for `IsVisible`)                     | Supports `ConverterParameter=Invert`           |
| `BoolToOpacity`         | `BoolToOpacityConverter`       | `bool` → `double`                                                | `true` (locked) → `0.4`, `false` → `1.0`      |

---

## 4. Icon Usage (Material Icons) `[Style:Icons]`

All icons use `Material.Icons.Avalonia` (`mi:MaterialIcon`).

### 4.1 Sidebar & List

| Icon Kind               | Size    | Color                    | Context                      |
|--------------------------|---------|--------------------------|------------------------------|
| `FormatListBulleted`     | 12×12   | White (on blue bg)       | Sidebar list item badge      |
| `Plus`                   | 12×12   | White (on blue bg)       | Sidebar "New List" button    |

### 4.2 Title Bar `[Style:TitleBar]`

All title bar icons use a unified **11×11** size.

| Icon Kind                      | Color              | Context                   |
|---------------------------------|---------------------|---------------------------|
| `PictureInPictureBottomRight`   | `TitleBarActionFg`  | Floating window toggle    |
| `Play`                          | `TitleBarActionFg`  | Start new run             |
| `CogOutline`                    | `TitleBarActionFg`  | Settings                  |
| `WindowMinimize`                | `SecondaryText`     | Minimize button           |
| `WindowMaximize`                | `SecondaryText`     | Maximize button           |
| `WindowClose`                   | `SecondaryText`     | Close button              |

### 4.3 Content Area

| Icon Kind               | Size    | Color                         | Context                       |
|--------------------------|---------|--------------------------------|-------------------------------|
| `Check`                  | 14×14   | White                          | Rename confirm button        |
| `DeleteOutline`          | 14×14   | `SecondaryText`                | Delete list/item             |
| `Plus`                   | 14×14   | White                          | Add task button              |
| `ChevronRight`           | 16×16   | `SecondaryText`                | Open sub-list                |
| `FileTreeOutline`        | 14×14   | `SecondaryText`, opacity 0.5   | Add sub-tasks                |
| `CheckCircleOutline`     | 20×20   | Converter-driven              | Done checkbox (via `DoneToIcon`) |
| `CircleOutline`          | 20×20   | Converter-driven              | Undone checkbox (via `DoneToIcon`) |
| (NodeType-driven)        | 12×12   | White (on blue bg)             | Non-task node icon           |

### 4.4 Running Tab

| Icon Kind               | Size    | Color              | Context                       |
|--------------------------|---------|---------------------|-------------------------------|
| `Close`                  | 14×14   | `SecondaryText`     | Remove instance              |
| `ArchiveArrowDown`       | 14×14   | `AccentGreenBrush`  | Archive banner icon          |

### 4.5 History Tab

| Icon Kind               | Size    | Color              | Context                       |
|--------------------------|---------|---------------------|-------------------------------|
| `History`                | 12×12   | White (on purple bg)| History log badge            |

### 4.6 Floating Window

| Icon Kind               | Size    | Color              | Context                       |
|--------------------------|---------|---------------------|-------------------------------|
| `Plus`                   | 12×12   | `IconForeground`    | Add new run                  |
| `Close`                  | 12×12   | `IconForeground`    | Close floating               |
| `ChevronLeft`            | 12×12   | `IconForeground`    | Prev instance                |
| `ChevronRight`           | 12×12   | `IconForeground`    | Next instance                |
| `Play`                   | 14×14   | (inherit)           | Start run inside flyout      |
| `CheckCircleOutline`     | 16×16   | `TertiaryText`      | Done checkbox                |
| `CircleOutline`          | 16×16   | `TertiaryText`      | Undone checkbox              |
| `ArchiveArrowDown`       | 14×14   | `AccentGreenBrush`  | Archive banner               |

---

## 5. Acrylic Materials `[Style:Acrylic]`

All acrylic TintColors are **theme-aware** via code-behind `ApplyAcrylicForCurrentTheme()`.

| Window          | Element            | TintColor (Light/Dark)       | TintOpacity | MaterialOpacity |
|-----------------|--------------------|------------------------------|-------------|-----------------|
| `MainWindow`    | Title bar sidebar  | `#F7F7FA` / `#1E1E20`       | `0.8`       | `0.7`           |
| `MainWindow`    | Sidebar body       | `#F7F7FA` / `#1E1E20`       | `0.8`       | `0.7`           |
| `MainWindow`    | New list button bg | `#FFFFFF` / `#2C2C2E`        | `0.85`      | `0.6`           |
| `SettingsWindow`| Sidebar            | `#F7F7FA` / `#1E1E20`       | `0.8`       | `0.7`           |
| `FloatingWindow`| Header bar         | `#F0F0F2` / `#1E1E20`       | `0.65`      | `0.45`          |

---

## 6. Shadows `[Style:Shadows]`

| BoxShadow                | Usage                              |
|--------------------------|------------------------------------|
| `0 1 3 0 #1A000000`     | Card subtle shadow (`Border.Card`) |
| `0 4 12 0 #33000000`    | FloatingBall strong shadow         |
| `0 3 12 0 #28000000`    | FloatingWindow outer shadow        |
| `0 4 16 0 #28000000`    | ConfirmDialog outer shadow         |
| `0 8 24 0 #40000000`    | Drag ghost shadow (code-behind)    |

---

## 7. Typography `[Style:Typography]`

| Context                 | FontSize | FontWeight | Notes                             |
|--------------------------|----------|------------|-----------------------------------|
| Window title ("OnFlight")| 13       | Bold       |                                   |
| Section header           | 24       | SemiBold   | `SectionHeader` class             |
| List name display        | 24       | SemiBold   |                                   |
| Rename TextBox           | 20       | SemiBold   |                                   |
| ConfirmDialog title      | 17       | SemiBold   |                                   |
| Panel tab headers        | 15       | SemiBold   | `PanelTabs` class                 |
| Sidebar "New List"       | 14       | SemiBold   |                                   |
| Add task TextBox         | 14       | (default)  |                                   |
| Sidebar list item        | 13       | (default)  |                                   |
| Breadcrumb link          | 13       | (default)  | `Breadcrumb` class                |
| Todo item title          | 13       | (default)  |                                   |
| Config field labels      | 13       | (default)  |                                   |
| Running instance name    | 13       | SemiBold   |                                   |
| History detail           | 13       | (default)  |                                   |
| Floating header title    | 13       | Bold       |                                   |
| Floating item title      | 12       | (default)  |                                   |
| Progress counter         | 12       | (default)  |                                   |
| Flyout text              | 12       | SemiBold   |                                   |
| Config helper text       | 11       | (default)  |                                   |
| Running timestamp        | 11       | (default)  |                                   |
| Floating indicator       | 11       | (default)  |                                   |
| Archive banner text      | 11       | (default)  |                                   |
| Draft badge "TMP"        | 10       | Bold       |                                   |
| Fork child hint          | 10       | Italic     |                                   |
| History op-type          | 10       | (default)  |                                   |
| Floating fork tag        | 9        | (default)  |                                   |

---

## 8. Layout Structure `[Style:Layout]`

### MainWindow Grid
```
Row 0 (36px)  : Custom Title Bar
Row 1 (*)     : Main Content

Col 0 (220px, 160–320) : Sidebar (acrylic)
Col 1 (Auto)           : GridSplitter
Col 2 (*, min 300)     : Content area
Col 3 (Auto)           : GridSplitter
Col 4 (340px, min 260) : Right panel (Config/Running/History tabs)
```

### SettingsWindow Grid
```
Col 0 (200px)  : Sidebar (acrylic, spans Row 0 + Row 1)
Col 1 (*)      : Content area

Row 0 (36px)   : Title bar
Row 1 (*)      : Left: category ListBox; Right: settings content + Save/Reset bar
```

### FloatingWindow
```
Width: 360, MaxHeight: 720, SizeToContent: Height
Border: CardBackground, CornerRadius: 12, Margin: 16, BoxShadow: 0 3 12 0 #28000000
  ├─ ClipBorder (ClipToBounds, same CornerRadius)
  │   ├─ Header (acrylic, draggable)
  │   └─ ScrollViewer content
```

### ConfirmDialog
```
Width: 400, SizeToContent: Height
Background: Transparent, SystemDecorations: None
Border: CardBackground, CornerRadius: 12, Padding: 24,20, Margin: 8
BoxShadow: 0 4 16 0 #28000000
```

---

## 9. Theme Switching `[Style:ThemeSwitching]`

### 9.1 Configuration

Theme preference stored in `settings.json` under `appearance.themeMode`:

| Value    | Avalonia `RequestedThemeVariant` | Behavior                                   |
|----------|----------------------------------|---------------------------------------------|
| `System` | `ThemeVariant.Default`           | Follows OS light/dark setting (default)     |
| `Light`  | `ThemeVariant.Light`             | Forces light theme                          |
| `Dark`   | `ThemeVariant.Dark`              | Forces dark theme                           |

### 9.2 What Responds Automatically vs. Manually

| Mechanism                     | Scope                                            |
|-------------------------------|--------------------------------------------------|
| `DynamicResource` bindings    | All color tokens in §1 — **automatic**           |
| `ThemeDictionaries`           | Light/Dark resource sets — **automatic**          |
| `ExperimentalAcrylicMaterial.TintColor` | **Manual** — code-behind per window  |
| `DoneToTextBrush` / `DoneToBrush` converters | **Manual** — reads `ActualThemeVariant` at convert time |

### 9.3 Windows with Manual Acrylic Handling

| Window           | Code-behind method              | Acrylic elements updated                        |
|------------------|---------------------------------|-------------------------------------------------|
| `MainWindow`     | `ApplyAcrylicForCurrentTheme()` | `TitleBarSidebarAcrylic`, `SidebarBodyAcrylic`, `NewListBtnAcrylic` |
| `SettingsWindow` | `ApplyAcrylicForCurrentTheme()` | `SidebarAcrylic`                                |
| `FloatingWindow` | `ApplyAcrylicForCurrentTheme()` | `HeaderAcrylic`                                 |

---

## 10. Transitions `[Style:Transitions]`

| Target Property    | Duration | Easing        | Style Class      |
|---------------------|----------|---------------|------------------|
| `Opacity`           | 0.3s     | CubicEaseOut  | `Card`           |
| `RenderTransform`   | 0.3s     | CubicEaseOut  | `Card`           |
| `Background`        | 0.1s     | (default)     | `ListItem`       |
| `RenderTransform`   | 0.2s     | CubicEaseOut  | `ListItem`       |
| `Opacity`           | 0.15s    | (default)     | `ListItem`       |
| `Opacity`           | 0.2s     | (default)     | `IconBtn`        |
| `Background`        | 0.15s    | (default)     | `PlainIconBtn` (on PART_ContentPresenter) |
| `Background`        | 0.15s    | (default)     | `WinBtn` (on PART_ContentPresenter)       |
| `Background`        | 0.2s     | (default)     | `PanelTabs` (on PART_ContentPresenter)    |
| `Foreground`        | 0.2s     | (default)     | `PanelTabs` (on PART_ContentPresenter)    |
| `Opacity`           | 0.15s    | (default)     | `ScrollBar Thumb`|

### 10.1 Slide Transition Animation `[Style:SlideTransition]`

All view-switch animations use the unified `SlideTransitionHelper` with identical parameters. Implemented in `SlideTransitionHelper.cs` (code-behind, not XAML Transitions).

**Shared parameters (used by both center list and right panel):**

| Parameter      | Slide-out        | Slide-in          |
|----------------|------------------|--------------------|
| Direction      | Left −16px       | From right +16px → 0 |
| Duration       | 140ms            | 160ms              |
| Easing         | `CubicEaseIn`    | `CubicEaseOut`     |
| Opacity        | 1 → 0            | 0 → 1              |

**Applies to:**
- **Center item list**: slide-out + slide-in on list switch / breadcrumb navigation / attach running instance
- **Right panel tabs**: slide-out old tab content + slide-in new tab content on tab switch

**Architecture:**
- `MainViewModel.OnBeforeItemsSwitch` / `OnAfterItemsLoaded` — `Func<Task>` callbacks invoked around `Items` reload
- `MainWindow.axaml.cs` registers callbacks to run `SlideTransitionHelper.SlideOutAsync` / `SlideInAsync`
- `PanelTabs.SelectionChanged` runs `SlideOutAsync` on old tab content, then `SlideInAsync` on new tab content

---

## 11. Inline Colors Reference `[Style:InlineColors]`

Remaining hardcoded colors that are **not** resource tokens:

| Color       | Usage                                      | Theme-aware? |
|-------------|--------------------------------------------|--------------|
| `#BBBBBB`   | CheckBtn pressed bg/fg                     | No (same both themes) |
| `White`     | Icon foreground on blue badges             | No (always on colored bg) |
| `#FF3B30`   | ConfirmDialog destructive button (code-behind) | No (accent color) |
