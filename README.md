# Project Desk

Project Desk is a Visual Studio 2022 extension with a visual panel for opening solutions, creating projects from Visual Studio templates, building the active solution, and managing files and folders.

## Use

1. Install the **Visual Studio extension development** workload in Visual Studio 2022.
2. Open `ProjectDesk.csproj` in Visual Studio and build the project.
3. Install `bin\Debug\ProjectDesk.vsix` (or the matching configuration output).
4. In Visual Studio, open **Tools > Project Desk**.
5. Open a solution or choose a folder. Double-click folders to browse and double-click files to open them in Visual Studio.
6. File deletion asks for confirmation and moves the item to the Recycle Bin.

Creating a project requires an open, saved solution and a project template (`.zip` or `.vstemplate`). Build results with errors are reported in the panel; detailed diagnostics remain in Visual Studio's Output window.

## Publish

Build the VSIX, create a publisher account in Visual Studio Marketplace, then upload the generated `.vsix` file through the Marketplace publisher portal. Update the extension identity and publisher in `source.extension.vsixmanifest` before publishing.
