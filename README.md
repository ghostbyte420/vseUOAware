## vseUOAware </br>
```An MCP Agent That Allows Visual Studio Copilot to Read UO Client Files```</br>

---

## Why Use This? </br>

With this tool you can ask questions like *"find the item ID for sandals"*, *"render gump 0"*, or *"show me an ASCII map of facet 1"* right in chat, and get 
real answers backed by actual decoded client data instead of guesses.</br>

Working with legacy UO client formats (`.mul`, `.uop`) normally means digging through outdated tools, forum posts, or reverse-engineered reference source just
to answer a simple question like "what's the ID for this item?" or "what does gump 12 look like?". vseUOAware puts that knowledge directly in your AI coding assistant's hands:

- **No more guessing IDs.** Ask in plain English and get exact tile/item/gump/sound/body IDs pulled straight from the real client files.</br></br>
- **Visual, not just textual.** Art, gumps, hues, lights, textures, fonts, and full creature animations render to real PNGs you can open immediately — critical for anything binary/graphical that plain text can't convey.</br></br>
- **One tool, every legacy format.** Classic `.idx`/`.mul` pairs and the newer hash-indexed `.uop` archives are both fully supported side by side, so you don't need separate tooling depending on client era.</br></br>
- **Safe by design.** Everything is strictly read-only against your original client files — nothing here can corrupt your source assets, making it safe to explore, script against, and build tooling/content on top of.</br></br>
- **Built for real development work**, not just browsing — whether you're scaffolding a new item, verifying an animation, auditing tiledata flags, or laying groundwork for larger systems (including next-gen asset pipelines), the data your agent needs is one question away.

---

## Available tools

39 tools are currently exposed, spanning:

| Category | Toolbox |
|---|---|
| Directory/file browsing | `get_saved_uo_directory`, `set_uo_directory`, `list_uo_files`, `read_uo_text_file` |
| Classic & UOP archive inspection | `list_mul_entries`, `read_mul_entry_preview`, `list_uop_entries`, `read_uop_entry_preview` |
| Tile/item metadata | `get_land_tile`, `get_item_tile`, `find_item_id_by_name` |
| Visual rendering | `render_art_tile`, `render_gump`, `render_hue_swatch`, `render_light`, `render_texture`, `render_ascii_text`, `render_multi_map` |
| Animation | `list_animation_bodies`, `list_defined_actions`, `render_animation_frames`, `get_item_animation_data` |
| Map | `render_map_ascii`, `inspect_map_region`, `inspect_map_coordinate` |
| Semantic/reference data | `get_multi`, `list_skills`, `list_skill_groups`, `get_localized_string`, `search_localized_strings`, `search_speech`, `list_verdata_patches`, `list_ascii_fonts`, `get_radar_color` |
| Sound | `search_sounds`, `export_sound` |
| Lighting | `get_light_count` |
| Demo | `get_random_number` |

## How To Configure...

Place the project on your desktop or any other location, and then open up the `.mcp.json` file in a text editor and replace `<PATH TO PROJECT DIRECTORY>` with the full path 
to this projects directory on your machine, for example:

```json
{
  "servers": {
    "vseUOAware": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "E:\\Development\\Source Code\\Visual Studio\\MCP\\vseUOAware\\vseUOAware\\vseUOAware.csproj"
      ]
    }
  }
}
```

Then place the `.mcp.json` file in the same directory as the Visual Studio `.slnx` file for the project you are working on. Restart Visual Studio and make sure that </br>
your setup is similar to the following screenshots:

![Alt Text](https://uoavox.studio/site_image/softwaredl/vseUOAware/vse1.png?version=2)
![Alt Text](https://uoavox.studio/site_image/softwaredl/vseUOAware/vse2.png?version=2)


## Cross-Platform?

The MCP server is built as a self-contained application and does not require the .NET runtime to be installed on the target machine.
However, since it is self-contained, it must be built for each target platform separately.
By default, the template is configured to build for:
* `win-x64`
* `win-arm64`
* `osx-arm64`
* `linux-x64`
* `linux-arm64`
* `linux-musl-x64`

If your users require more platforms to be supported, update the list of runtime identifiers in the project's `<RuntimeIdentifiers />` element.</br>
See [aka.ms/nuget/mcp/guide](https://aka.ms/nuget/mcp/guide) for the full guide.
