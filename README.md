# Nostrid - A Nostr client

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)
[![Donate: LN](https://img.shields.io/badge/Donate-LN-green)](https://legend.lnbits.com/tipjar/786)

---

Nostrid is a multi-platform, open-source Nostr client. It is written in .NET and runs in Android, Windows and MacOS (it should run in iOS too but there are no binaries yet).

Also a [web version](https://web.nostrid.app/) is available. It runs completely on the browser, and it can even be installed locally as a [PWA](https://en.wikipedia.org/wiki/Progressive_web_app).

For a quick start check out the [Installation](#Installation) section below.

## Features

- [x] Multiaccount with simple switching
- [x] NIP-07 extensions, e.g. nos2x (Web/PWA version only)
- [x] NIP-05 profiles
- [x] Lightning Network (pay button and profile setup)
- [x] Text notes with support for markdown (receive and send)
- [x] Upload media files (uses third-party services [nostr.build](https://nostr.build/), [void.cat](https://void.cat/) and [nostrimg.com](https://nostrimg.com/))
- [x] Delete notes
- [x] NIP-13 PoW (receive/filter and send)
- [x] Reactions (receive and send)
- [x] Hashtags (send and search)
- [x] Customizable feeds based on hashtags
- [x] Username and profile images (read and update)
- [x] Follows (read and update)
- [x] Followers (read)
- [x] Bech32 (`npub`, `note`, `nsec`)
- [x] Automatic relay management (no user intervention is required)
- [x] Manual relay management
- [x] Local cache of events
- [x] Notification of unread mentions
- [x] Display embedded images, videos and audios
- [x] Reposts (aka Boosts)
- [x] Embedded mentions of other accounts and notes - PARTIAL (only links, no previews)
- [x] Channels - PARTIAL (can join and participate but can't create channels yet)
- [ ] Direct messages - SOON
- [ ] NIP-26 delegation - SOON

## Supported NIPs

- NIP-01: Basic protocol flow description ✅
- NIP-02: Contact List and Petnames ✅
- NIP-03: OpenTimestamps Attestations for Events
- NIP-04: Encrypted Direct Message
- NIP-05: Mapping Nostr keys to DNS-based internet identifiers ✅
- NIP-06: Basic key derivation from mnemonic seed phrase
- NIP-07: window.nostr capability for web browsers ✅ (Web/PWA version only)
- NIP-08: Handling Mentions ✅
- NIP-09: Event Deletion ✅
- NIP-10: Conventions for clients' use of e and p tags in text events. ✅
- NIP-11: Relay Information Document ✅
- NIP-12: Generic Tag Queries ✅
- NIP-13: Proof of Work ✅
- NIP-14: Subject tag in text events.
- NIP-15: End of Stored Events Notice ✅
- NIP-16: Event Treatment
- NIP-19: bech32-encoded entities ✅
- NIP-20: Command Results
- NIP-21: nostr: URL scheme
- NIP-22: Event created_at Limits
- NIP-25: Reactions ✅
- NIP-26: Delegated Event Signing
- NIP-28: Public Chat ✅
- NIP-33: Parameterized Replaceable Events
- NIP-36: Sensitive Content
- NIP-40: Expiration Timestamp
- NIP-42: Authentication of clients to relays
- NIP-50: Keywords filter ✅
- NIP-56: Reporting
- NIP-65: Relay List Metadata

## Screenshots

![Nostrid Android](https://raw.githubusercontent.com/lapulpeta/Nostrid-media/main/nostrid-mobile1.jpg)
![Nostrid Android](https://raw.githubusercontent.com/lapulpeta/Nostrid-media/main/nostrid-mobile2.jpg)
![Nostrid Android](https://raw.githubusercontent.com/lapulpeta/Nostrid-media/main/nostrid-mobile3.jpg)
![Nostrid Windows](https://raw.githubusercontent.com/lapulpeta/Nostrid-media/main/nostrid1.jpg)
![Nostrid Windows](https://raw.githubusercontent.com/lapulpeta/Nostrid-media/main/nostrid2.jpg)
![Nostrid Windows](https://raw.githubusercontent.com/lapulpeta/Nostrid-media/main/nostrid3.jpg)

### Technologies & Frameworks

* [.NET 7.0](https://github.com/dotnet/runtime)
* [MAUI](https://github.com/dotnet/maui)
* [Blazor](https://github.com/dotnet/blazor)
* [Bootstrap](https://getbootstrap.com/)
* [Entity Framework](https://github.com/dotnet/efcore)
* [SQLite-net](https://github.com/praeclarum/sqlite-net)
* [NNostr](https://github.com/Kukks/NNostr)
* [HtmlSanitizer](https://github.com/mganss/HtmlSanitizer)
* [Jdenticon.Net](https://github.com/dmester/jdenticon-net)
* [Markdig](https://github.com/xoofx/markdig)
* [QRCoder](https://github.com/codebude/QRCoder)
* [Popper](https://popper.js.org/)

### Contributing

Feel free to open issues and send PRs.
Alternatively you can support the project by donating here:

* [Lightning Network](https://legend.lnbits.com/tipjar/786)
* [LNURL](https://legend.lnbits.com/lnurlp/link/VaE6ox)
* BTC: `bc1p3r5yx550jprj5zzg8arlrnaregvxsxuh5jfx95awex6shx0etcxsygks7r`
* XMR: `84qzqe1EW5fDvaRN9xj5HACTL6xmd1M4wNk1K1W29W5CVXuhpdVYXSU5ZfCpxJJw7MeLJyEybGSqgNwU3Rn9qm2PDKgKFmD`

## Getting Started

### Requirements

* Android
    * 7.0 or higher

* Windows
    * 10.0.19041.0 or higher

* MacOS
    * 11 or higher

### Installation

You can find the binaries in the [Releases](https://github.com/lapulpeta/Nostrid/releases) section.

* Android
    * Download APK and install.

* Windows
    * Download ZIP package, unzip and run `Install.ps1` in PowerShell. Follow instructions.

* MacOS
    * Download PKG and install. Since the package is unsigned you may have to control-click and choose `Open with...` > `Installer`.

### Building

1. Install Visual Studio 2022 with support for `.NET Multi-platform App UI development` (and optionally `.NET Webassembly Build Tools` for Web project)
2. Clone this repository
3. Make sure `git` is in your `PATH` system variable
4. Run `git submodule update` to restore custom libraries
5. Open solution in Visual Studio and run
6. For Android: run `dotnet build -c Release -f net7.0-android` in `Nostrid` inner project
7. For MacOS: run `dotnet publish -c Release -f net7.0-maccatalyst` in `Nostrid` inner project

## Authors

* lapulpeta `npub14uc57wfq2zd0g3qh5lpvkq2svvkjl9fruzyxnz9zh95ev2japw7ql2g0sq`
* danielmangia `npub1aftaccjtz06z0naqxdlavwutw5ccr2x4wx3wrld7s5yd9j4kqwuq3kvrja`

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE.md) file for details
