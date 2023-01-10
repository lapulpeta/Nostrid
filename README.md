# Nostrid - Nostr Client

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)
[![Contribute: LN](https://img.shields.io/badge/Contribute-LN-green)](https://legend.lnbits.com/tipjar/786)

---

Nostrid is a multi-platform, open-source Nostr client.
It is written in .NET and runs in Windows and Android (in theory it should run in iOS and macOS too but it hasn't been tested yet).

## Features

- [x] Multiaccount (generate and restore existing account)
- [x] Read and send notes
- [x] Read and send reactions
- [x] Delete notes
- [x] Hashtags (send and search)
- [x] Custom feeds based on hashtags
- [x] Username and profile images (read and update)
- [x] Follows (read and update)
- [x] Bech32 (`npub`, `note`)
- [x] Automatic relay management (no user intervention is required)
- [x] Local cache of events
- [x] Notification of mentions for current account
- [x] Embedded mentions of other accounts and notes - PARTIAL
- [x] Reposts (aka Boosts) - PARTIAL
- [ ] Embedded links, images and videos - SOON
- [ ] NIP-05 profiles - SOON
- [ ] Direct messages - SOON
- [ ] Channels - SOON
- [ ] Manual/advanced relay management - SOON
- [ ] Lightning Network payments - SOON

### Technologies

* .NET 7.0 MAUI Blazor
* [Bootstrap](https://getbootstrap.com/)
* [LiteDB](https://github.com/mbdavid/LiteDB)
* [NNostr](https://github.com/Kukks/NNostr)
* [HtmlSanitizer](https://github.com/mganss/HtmlSanitizer)

### Contributing

Feel free to open issues and send PRs.
Alternatively you can support the project by donating here:

* [Lightning Network](https://legend.lnbits.com/tipjar/786)
* [LNURL](https://legend.lnbits.com/lnurlp/link/VaE6ox)
* BTC: `bc1p3r5yx550jprj5zzg8arlrnaregvxsxuh5jfx95awex6shx0etcxsygks7r`
* XMR: `84qzqe1EW5fDvaRN9xj5HACTL6xmd1M4wNk1K1W29W5CVXuhpdVYXSU5ZfCpxJJw7MeLJyEybGSqgNwU3Rn9qm2PDKgKFmD`

## Getting Started

### Requirements

* Windows
    * 10.0.19041.0 or higher

* Android
    * Android 7.0 or higher

### Installation

You can find the binaries in the [Releases](https://github.com/lapulpeta/Nostrid/releases) section.

* Windows
    * Download ZIP package, unzip and run `Install.ps1` in PowerShell. Follow instructions.

* Android
    * Download APK and install.

### Building

1. Install Visual Studio 2022 with support for .NET Multi-platform App UI development
2. Clone this repository
3. Make sure `git` is in your `PATH` system variable
4. Run `git submodule update` to restore custom NNostr library
5. Open solution in Visual Studio and run

## Authors

* lapulpeta `npub14uc57wfq2zd0g3qh5lpvkq2svvkjl9fruzyxnz9zh95ev2japw7ql2g0sq`
* danielmangia `npub1aftaccjtz06z0naqxdlavwutw5ccr2x4wx3wrld7s5yd9j4kqwuq3kvrja`

## License

This project is licensed under the MIT License - see the LICENSE.md file for details
