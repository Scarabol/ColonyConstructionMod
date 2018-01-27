# important variables
modname = Construction
versionmajor = 5.0
versionminor = 2
compatible_cs = "0.5.3"
zip_files_extra = "$(moddir)/assets/" "$(moddir)/blueprints/"

fullname = Colony$(modname)Mod
moddir = Scarabol/$(modname)
zipname = $(fullname)-$(version)-mods.zip
dllname = $(modname).dll

define MODINFO_JSON
[
  {
    "name" : "scarabol.$(shell echo $(modname) | tr A-Z a-z)",
    "version" : "$(version)",
    "dllpath" : "$(dllname)",
    "enabled" : true,
    "compatibleversions" : [
      $(compatible_cs)
    ]
  }
]
endef
export MODINFO_JSON

release_notes = '{"tag_name": "$(version)", "name": "$(fullname) $(version)", "body": "\#\# Changelog\nComing soon. See commits for details...\n\n\#\# Compatible with Colony Survival $(shell echo $(compatible_cs) | sed s/\"//g )\n\n\#\# Installation\n**This mod must be installed on server side!**\n* download the *$(zipname)* or build it from source code, see README for details.\n* place the unzipped *Scarabol* folder inside your *ColonySurvival/gamedata/mods/* directory, like\n*ColonySurvival/gamedata/mods/Scarabol/*"}'

version = $(versionmajor).$(versionminor)
nextversionminor = $(shell expr $(versionminor) + 1)
nextversion = $(versionmajor).$(nextversionminor)
#
# actual build targets
#

default:
	mcs /target:library -r:../../../../colonyserver_Data/Managed/Assembly-CSharp.dll -r:../../Pipliz/APIProvider/APIProvider.dll -r:../../../../colonyserver_Data/Managed/UnityEngine.dll -out:"$(dllname)" -sdk:2 src/*.cs

clean:
	rm -f "$(dllname)"

all: clean default

modinfo:
	echo "$$MODINFO_JSON" > "modInfo.json"

zip: default modinfo
	rm -f "$(zipname)"
	cd ../../ && zip -r "$(moddir)/$(zipname)" "$(moddir)/modInfo.json" "$(moddir)/$(dllname)" $(zip_files_extra)

release: zip
	git push
	git tag "$(version)" && git push --tags
	make publish
	make upload
	make incversion

publish:
	curl --user "scarabol@gmail.com" --data $(release_notes) "https://api.github.com/repos/Scarabol/$(fullname)/releases"

upload:
	curl --user "scarabol@gmail.com" --data-binary @"$(zipname)" -H "Content-Type: application/octet-stream" "https://uploads.github.com/repos/Scarabol/$(fullname)/releases/$(shell curl -s "https://api.github.com/repos/Scarabol/$(fullname)/releases/tags/$(version)" | jq -r '.id')/assets?name=$(shell basename $(zipname))"

incversion:
	sed -i "s/ $(version) / $(nextversion) /" src/*
	sed -i "s/versionminor = $(versionminor)/versionminor = $(nextversionminor)/" makefile
	git add src/* makefile ; git commit -m "increase version to $(nextversion)"

client: default
	cd ../../../../ && ./colonyclient.x86_64

server: default
	cd ../../../../ && ./colonyserver.x86_64

