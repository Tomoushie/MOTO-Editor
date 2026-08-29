Organisation des assets

Placez les fichiers suivants (d'après vos tailles 512/128/32) :

Moto.Editor/
├── Resources/
│   ├── AppIcon/
│   │   └── appicon.png          ← logo 512×512 (fond sombre inclus)
│   ├── Splash/
│   │   └── splash.png           ← logo centré sur fond #0B1526
│   └── Images/
│       ├── moto_logo.png        ← 512×512 (accueil, à propos)
│       ├── moto_logo_128.png    ← 128×128 (badges, panneaux)
│       └── moto_logo_32.png     ← 32×32  (sidebar, petits badges)
└── Platforms/
    └── Windows/
        └── appicon.ico          ← conversion du 512 en .ico (multi-tailles 256/128/64/48/32/16)

Pour générer appicon.ico : utilisez un convertisseur PNG→ICO multi-tailles (ex. magick convert moto_logo.png -define icon:auto-resize=256,128,64,48,32,16 appicon.ico).
