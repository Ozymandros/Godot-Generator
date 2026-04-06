# Blazor ↔ Unity-Generator generic component parity

Source: [Unity-Generator `frontend/src/components/generic/`](https://github.com/Ozymandros/Unity-Generator/tree/main/frontend/src/components/generic/) (Vue + TS).

| Unity (props / behavior) | Blazor |
|--------------------------|--------|
| `BaseField`: label, error, help, required, id | `BaseField.razor`: Label, ValidationMessage, Help, Required; id via child controls |
| `SmartField`: type text/number/password/email/textarea/select/checkbox, options, rows, min/max/step | `SmartField.razor`: Text, Multiline, Password (+ extend as needed) |
| `PromptInputSection`: prompt + provider + options record | `PromptInputSection.razor`: prompt only; provider/model live on `GenerationWorkspace` until unified |
| `ModelManagerModal` | `ModelManagerModal.razor` (placeholder dialog) |
| Advanced accordion (per screen) | `AdvancedOptionsSection.razor` (temperature, API key, system prompt, GDScript/C#) |

Option keys for `GenerateRequest.Options` are in `GenerationOptionKeys` (aligned with `ModalityTurnComposer` / orchestration).
