# How to build from source

Nostrid consists of several projects:

- `Nostrid.Core`: Library that contains most of the logic. This project is used by all the other projects.
- `Nostrid`: MAUI application for running Nostrid in Windows x64, Android, MacOS (MacCatalyst) and iOS
- `Nostrid.Web`: Web version of Nostrid
- `Nostrid.Photino`: Cross-platform desktop version of Nostrid that works in Windows (including Arm platform), Linux and MacOS

For Windows it's recommended to install [Visual Studio 2022](https://visualstudio.microsoft.com/vs/community/). For other platforms you can use .NET CLI ([how to install .NET SDK 7.0](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)).

## MAUI: Building for Windows

- Make sure Visual Studio is installed with option `.NET Multi-platform App UI development`, otherwise you will have run `dotnet workload restore` in the command line in the directory of project `Nostrid`
- Make sure Git is installed and added to your `PATH`

From Visual Studio:

1. Open solution
2. Right click on project `Nostrid`
3. To run: select `Debug` > `Start new instance`
4. To generate installer: select `Publish...` and follow the instructions

## MAUI: Building for MacOS, Android and iOS

- You can build for Android in Windows. For iOS and MacOS you need a Mac.
- You may be prompted to install workloads by running `dotnet workload restore`
- Make sure Git is installed and added to your `PATH`
- MacOS requires the project to run in Debug mode or else the WebView element doesn't appear to work

1. In the command line go to project `Nostrid`
2. For Android: run `dotnet publish -c Release -f net7.0-android`
3. For MacOS: run `dotnet publish -c Debug -f net7.0-maccatalyst`
4. For iOS: run `dotnet publish -c Release -f net7.0-ios` (refer to [this article](https://learn.microsoft.com/en-us/dotnet/maui/ios/deployment/?view=net-maui-7.0) for details on how to setup your environment)

## Photino: Building for Linux, Windows Arm and other desktop platforms

- For these platforms we use [Photino](https://www.tryphotino.io/)
- Make sure Git is installed and added to your `PATH`

From Visual Studio:

1. Open solution
2. Right click on project `Nostrid.Photino`
3. To run: select `Debug` > `Start new instance`
4. To generate binaries: select `Publish...` and follow the instructions

From CLI:

1. In the command line go to project `Nostrid.Photino`
2. Create file `.\Properties\PublishProfiles\FolderProfile.pubxml`
3. Add this content (adjust `RuntimeIdentifier` as needed according to desired platform):
```
<?xml version="1.0" encoding="utf-8"?>
<!--
https://go.microsoft.com/fwlink/?LinkID=208121.
-->
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <Platform>Any CPU</Platform>
    <PublishDir>bin\Release\net7.0\publish\linux-x64\</PublishDir>
    <PublishProtocol>FileSystem</PublishProtocol>
    <_TargetId>Folder</_TargetId>
    <TargetFramework>net7.0</TargetFramework>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
  </PropertyGroup>
</Project>
```
4. Run `dotnet publish -p:PublishProfile=FolderProfile`

## Building for Web

- Make sure Visual Studio is installed with component `.NET Webassembly Build Tools`
- Make sure Git is installed and added to your `PATH`

From Visual Studio:

1. Open solution
2. Right click on project `Nostrid.Web`
3. Select `Publish...` and follow the instructions