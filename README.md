# Armoryeet

Armoryeet is a Dalamud plugin that moves unused armoury chest gear back to inventory.

It scans your gearsets, finds armoury chest items that are not referenced by any gearset, and moves only those items when requested.

## Install

Add this custom plugin repository in Dalamud:

```text
https://raw.githubusercontent.com/Korokatto/Armoryeet/main/repo.json
```

In game:

```text
/xlsettings -> Experimental -> Custom Plugin Repositories
```

Paste the URL, press the plus button, save, then install Armoryeet from `/xlplugins`.

## Commands

```text
/armoryeet     Open Armoryeet
/armouryeet    Open Armoryeet
/yeet          Open Armoryeet or run /yeet help
/yeetsettings  Open settings
```

Useful `/yeet` actions:

```text
/yeet scan
/yeet chest
/yeet weapons
/yeet armor
/yeet accessories
/yeet mainhand
/yeet offhand
/yeet status
/yeet stop
```

## Build

```bash
dotnet build src/Armoryeet -c Release
```

Release package:

```text
src/Armoryeet/bin/Release/Armoryeet/latest.zip
```
