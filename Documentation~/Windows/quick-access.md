# Quick Access window

**Window > Quick Access ⚡**, or the **⚡** button on the main toolbar.

Quick Access gathers the objects you keep hunting for into one tree. Mark a component or
ScriptableObject class with `[QuickAccess]` and every instance of it, in the open scenes and in the
project, is listed here, ready to select and edit.

## Example

```csharp
[QuickAccess("Spawners")]
public class EnemySpawner : MonoBehaviour { … }

[QuickAccess]
public class GameSettings : ScriptableObject { … }
```

The tree then shows **Spawners** with every `EnemySpawner` in the scene (scene name in brackets), and
**GameSettings** with every settings asset.

## Layout

- **Explorer** (left): one branch per marked type, named by the attribute's label or the class name,
  with its instances underneath and an item count. **▲ / ▼** collapse or expand everything.
- **Inspector** (right): the selected object's inspector, so you can edit it without changing Unity's
  selection. **▶** hides this panel (compact mode); the selection then shows in Unity's own Inspector.
- **All components:** for a GameObject, show every component on it instead of only the marked one.
- **↺ Refresh:** rescans scenes and assets.
