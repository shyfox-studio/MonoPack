<h1 align="center">
<img src="https://raw.githubusercontent.com/shyfox-studio/branding/51d21485b8b524893dd84a735f05d1bd154066f0/icons/monopack/monopack.svg" alt="ShyFox.MonoPack Logo" width="256" />
<br />
MonoGame Project Packer
</h1>

<div align="center">

A dotnet tool that builds and packages MonoGame projects for Windows, macOS, and Linux.
<br />

[![License: MIT](https://img.shields.io/badge/LICENSE-MIT-f8723d)](LICENSE)

</div>

**MonoPack** is a dotnet tool used for MonoGame projects to package the game for Windows, Linux, and/or macOS.

## Features

- Can package for all three operating systems from any operating system.
- Creates a macOS application bundle (.app) automatically for distribution.
- Packages are compressed using zip compression for Windows packages and gz compression for Linux and macOS packages.

## Installation

Add this as a dotnet tool to your current MonoGame project. In the same directory as the `.csproj` file for your MonoGame project, run the following command in a command prompt or terminal:

```cs
dotnet tool install MonoPack
```

## Usage

```sh
MonoPack - MonoGame Project Packer
Usage: monopack [options] [project-path]

Options:
    -p --project <path>             Path to the .csproj file (optional if only one .csproj in current directory)
    -o --output <dir>               Output directory (default: [project]/bin/Packed)
    -r --runtime-identifier <rid>   Specify target runtime identifier(s) to build for.
    -i --info-plist <path>          Path to Info.plist file (required when packaging for macOS)
    -c --icns <path>                Path to .icns file (required when packaging for macOS)
    -e --executable <name>          Name of the executable file to set as executable.
    -v --verbose                    Enable verbose output
    -h --help                       Show this help message

Available runtime identifiers:
    win-x64     Windows
    linux-x64   Linux
    osx-x64     macOS 64-bit Intel
    osx-arm64   macOS Apple Silicon


Notes:
    If the --project path is not specified, an attempt will be made to locate
    the .csproj within the current directory.

    If a --runtime-identifier is not specified, a default one will be chosen
    based on the operating system.

    When the --runtime-identifier specified is osx-x64 and/or osx-arm64, paths
    to the Info.plist and the .icns file must be specified.  If they are not
    specified, an attempt is made to locate them in the current directory.

    Use --executable if your output assembly does NOT match the `.csproj` filename. For example if you
    have `MyGame.DesktopGL.csproj`, but you use `<AssemblyName>MyGame</AssemblyName>` in your csproj
    you will need to pass `-e MyGame` as an argument. Otherwise the package you get will be invalid.

Examples:
    monopack
    monopack -h
    monopack -p ./src/MyGame.csproj -o ./artifacts/builds -r win-x64 -r osx-x64 -r osx-arm64 -r linux-x64 -i ./Info.plist -c ./Icon.icns
    monopack -p ./src/MyGame.Desktop.csproj -e MyGame -o ./artifacts/builds -r win-x64 -r osx-x64 -r osx-arm64 -r linux-x64 -i ./Info.plist -c ./Icon.icns
```

> [!IMPORTANT]
> When executing the `monopack` command with no `-r` or `--runtime-identifier` flag(s), it will default to use the runtime identifier for the current operating system only.

> [!IMPORTANT]
> You can specify `-r osx-x64` for an Intel (x64) based macOS build only, or `-r osx-arm64` for an Apple Silicon (arm64) based macOS build only.
>
> If both are specified, then a universal build will be created.
>
> When executing just `monopack` on a macOS, a universal build is selected by default.
> When possible, you should package them on their intended system, such as through GitHub Actions if you don't have access to them yourself.

## Required Files for macOS

When packaging for macOS, it is required that you have an `Info.plist` and an Apple Icon (.icns) file in your project directory.  There is an example `Info.plist` in [/example/ExampleGame/Info.plist](/example/ExampleGame/Info.plist).  You can use this file and just replaced the required values for your game:

- **CFBundleExecutable**: Update the string to be the same name as your project file (.csproj) minus the extension.  For example, if your project file is named `MyGame.csproj` then you would use the following

    ```xml
    <key>CFBundleExecutable</key>
    <string>MyGame</string>
    ```

If your final executable name is different from your csproj, you will need to use that
value in the `CFBundleExecutable`. For example, if your project file is named `MyGame.DesktopGL.csproj` but your `AssemblyName` is set to `MyGame`. You will need to use
`MyGame` in the `CFBundleExecutable` value.

- **CFBundleIdentifier**: Update the string to the identifier for your game.

- **CFBundleName**: Update the string to be the same name as your project file (.csproj) minus the extension.  For example, if your project file is named `MyGame.csproj` then you would use the following:

    ```xml
    <key>CFBundleName</key>
    <string>MyGame</string>
    ```

For the `Icon.icns` file, you can find a default MonoGame one in at [/example/ExampleGame/Icon.icns](./example/ExampleGame/Icon.icns).  If you would like to create your own, there are tons of online tools for converting image files to the Apple Icon (icns) format.  Just google.

## License

MonoPack is licensed under the MIT License.  Please refer to [LICENSE](LICENSE) for full license text.
