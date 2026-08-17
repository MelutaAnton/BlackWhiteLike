---
name: urp-lit-specular-workflow
description: Exact URP/Lit shader property names and keywords for building a Specular-workflow material from an Editor script
metadata:
  type: project
---

Confirmed by reading `Library/PackageCache/com.unity.render-pipelines.universal@*/Shaders/Lit.shader`
and `Editor/ShaderGUI/ShadingModels/LitGUI.cs` in this project (Unity 6000.3.9f1, URP).

- `_WorkflowMode` (Float): `0` = Specular, `1` = Metallic (default `1`). Enum is
  `LitGUI.WorkflowMode` in the URP editor assembly (`Specular = 0`).
- Specular workflow textures/props: `_SpecColor` (Color), `_SpecGlossMap` (Texture2D, RGB=specular
  tint, A=gloss). Set `_SpecColor = Color.white` when a gloss map supplies tint so the map fully
  controls it.
- Albedo: `_BaseMap` (Texture2D) + `_BaseColor` (Color, MainColor).
- Normal map: `_BumpMap` (Texture2D). Must also `material.EnableKeyword("_NORMALMAP")` or the
  normal map is silently ignored at runtime (keyword drives the shader variant, not just the slot).
- Also enable `_SPECULAR_SETUP` when using specular workflow, and `_METALLICSPECGLOSSMAP` when a
  gloss map texture is assigned (mirrors `BaseShaderGUI.SetMaterialKeywords`).
- These keyword requirements are undocumented in the Inspector — a material built purely via
  `SetTexture`/`SetFloat` without the matching `EnableKeyword` calls compiles but renders as if the
  maps were absent (flat/no normal detail, no specular response).

Used in `Assets/Development/Scripts/Editor/Setup/GodHandPrefabSetupEditor.cs` (Hand_Neutral.mat).
Relevant to any future Editor script generating URP/Lit materials programmatically in this project.
