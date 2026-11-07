# Google Fonts typography

WinCC defaults to Lato Regular/Bold for a clean desktop appearance. VT323 Regular remains bundled as an optional retro typeface. Both render offline.

- Google Fonts: https://fonts.google.com/specimen/Lato
- Google Fonts stylesheet link: https://fonts.googleapis.com/css2?family=Lato:wght@400;700&display=swap
- Official source: https://github.com/google/fonts/tree/main/ofl/lato

WPF cannot load a CSS stylesheet as a font. The equivalent TTF files are embedded as resources, with the included SIL Open Font License. No font request is made when starting the app.

The Design font dropdowns also offer Space Mono, Share Tech Mono, Rajdhani and Bungee through **Get Google font**. Their regular TTF and OFL files are downloaded from `raw.githubusercontent.com/google/fonts/main/ofl` into `%LOCALAPPDATA%\WinCC\fonts`, then loaded locally without installing system fonts. Select **Apply design** after downloading. Installed fonts remain available offline.

## VT323

- Google Fonts: https://fonts.google.com/specimen/VT323
- Stylesheet: https://fonts.googleapis.com/css2?family=VT323&display=swap
- Official source: https://github.com/google/fonts/tree/main/ofl/vt323
- License: `VT323-OFL.txt` (SIL Open Font License), shipped alongside the app.
