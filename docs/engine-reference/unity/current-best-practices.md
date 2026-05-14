# Unity — Current Best Practices

Last verified: 2026-05-01 | Engine: Unity 6.3 LTS

Practices that are new or changed since the model's training data (~2023.x).

## URP 2D Rendering (Unity 6.x)

- **Use 2D Renderer asset** — set in the URP Asset's Renderer List. This enables 2D lights, shadow casters, and sprite-specific shaders
- **Render Graph is required** for any custom `ScriptableRendererFeature` — Compatibility Mode is removed in 6.3
- **On-Tile Post Processing** — enable "Tile-Only Mode" in URP settings for mobile. Reduces GPU bandwidth consumption significantly on Android/iOS tile-based GPUs
- **Bloom for mobile**: Use URP's built-in Bloom post-process volume. Bloom intensity must be tuned in-engine — do not rely on non-URP defaults
- **GPU Resident Drawer**: Enabled via URP settings. Automatically uses `BatchRendererGroup` to reduce draw calls via GPU instancing — beneficial for bolt sprites

## Sprite & 2D Workflow

```csharp
// Sprite Renderer — unchanged API
[SerializeField] private SpriteRenderer _spriteRenderer;  // field, not property

void Start()
{
    _spriteRenderer.color = Color.white;
    _spriteRenderer.sortingLayerName = "Bolts";
    _spriteRenderer.sortingOrder = 1;
}
```

## Object Lookup (CHANGED in 6.0)

```csharp
// WRONG — removed in 6.0
var managers = FindObjectsOfType<GameManager>();

// CORRECT — Unity 6.x
var managers = FindObjectsByType<GameManager>(FindObjectsSortMode.None);
var first = FindFirstObjectByType<GameManager>();
var any = FindAnyObjectByType<GameManager>();
```

## SerializeField (CHANGED in 6.3)

```csharp
// WRONG — compile error in 6.3
[SerializeField] public int MaxStack { get; set; }

// CORRECT option A — backing field
[SerializeField] private int _maxStack;
public int MaxStack => _maxStack;

// CORRECT option B — auto-property with field attribute
[field: SerializeField] public int MaxStack { get; private set; }
```

## Scriptable Renderer Features — Render Graph (REQUIRED in 6.3)

```csharp
// WRONG — SetupRenderPasses removed in 6.3
public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData) { }

// CORRECT — Render Graph API
public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
{
    renderer.EnqueuePass(m_Pass);
}
```

## Coroutines & Async (unchanged, but prefer async/await)

```csharp
// Coroutine (still valid)
private IEnumerator WaitAndLoad()
{
    yield return new WaitForSecondsRealtime(2f);  // wall-clock time, unaffected by timeScale
    LoadNext();
}

// async/await (preferred for new code)
private async void LoadAsync()
{
    await Task.Delay(2000);  // or use UniTask if package available
    LoadNext();
}
```

## Mobile Audio

```csharp
// AudioMixer volume control — unchanged API
[SerializeField] private AudioMixer _masterMixer;

public void SetSFXVolume(float normalizedVolume)
{
    // AudioMixer expects dB — convert from 0-1 linear
    float db = normalizedVolume > 0.001f ? Mathf.Log10(normalizedVolume) * 20f : -80f;
    _masterMixer.SetFloat("SFXVolume", db);
}
```

## PlayerPrefs — mobile persistence

```csharp
// PlayerPrefs for simple values (audio settings, quality tier)
PlayerPrefs.SetFloat("audio.sfx_volume", volume);
PlayerPrefs.GetFloat("audio.sfx_volume", 1.0f);  // second arg = default
PlayerPrefs.Save();  // call explicitly on mobile to flush
```

## Android Icon Requirements (6.3+)

- **Adaptive icons required** — round and legacy icon types deprecated
- Configure via Player Settings > Android > Adaptive Icons
- Foreground + background layers required

## Canvas / UI (unchanged for 2D games)

```csharp
// Screen Space - Overlay Canvas (HUD)
// Set via Inspector: Canvas component → Render Mode = Screen Space - Overlay
// Safe area handling for notch/home indicator:
var safeArea = Screen.safeArea;
var anchorMin = safeArea.position;
var anchorMax = safeArea.position + safeArea.size;
anchorMin.x /= Screen.width;
anchorMin.y /= Screen.height;
anchorMax.x /= Screen.width;
anchorMax.y /= Screen.height;
rectTransform.anchorMin = anchorMin;
rectTransform.anchorMax = anchorMax;
```

## Atomic File Write (Save System)

```csharp
// Write-then-rename for atomic save (prevents corruption)
var tmpPath = savePath + ".tmp";
File.WriteAllText(tmpPath, json);
if (File.Exists(savePath)) File.Delete(savePath);
File.Move(tmpPath, savePath);
```

## Quality Tier / Script Execution Order

- Set in **Edit > Project Settings > Script Execution Order**
- Drag singleton manager scripts to run before Default Time (negative order values)
- Example: QualityTierSystem at -100, SaveSystem at -90, GameStateManager at -50
