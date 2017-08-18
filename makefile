# important variables
modname = Construction
version = 1.4

moddir = Scarabol/$(modname)
zipname = Colony$(modname)Mod-$(version)-mods.zip
dllname = $(modname).dll

#
# actual build targets
#

default:
	mcs /target:library -r:../../../../colonyserver_Data/Managed/Assembly-CSharp.dll -r:../../Pipliz/APIProvider/APIProvider.dll -r:../../../../colonyserver_Data/Managed/UnityEngine.dll -out:"$(dllname)" -sdk:2 src/*.cs
	echo '{\n\t"assemblies" : [\n\t\t{\n\t\t\t"path" : "$(dllname)",\n\t\t\t"enabled" : true\n\t\t}\n\t]\n}' > modInfo.json

clean:
	rm -f "$(dllname)" "modInfo.json"

all: clean default

release: default
	rm -f "$(zipname)"
	cd ../../ && zip -r "$(moddir)/$(zipname)" "$(moddir)/modInfo.json" "$(moddir)/$(dllname)" "$(moddir)/capsule_permissions.json" "$(moddir)/assets/" "$(moddir)/blueprints/"

client: default
	cd ../../../../ && ./colonyclient.x86_64

server: default
	cd ../../../../ && ./colonyserver.x86_64

