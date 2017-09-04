# important variables
modname = Construction
version = 2.1.5

moddir = Scarabol/$(modname)
zipname = Colony$(modname)Mod-$(version)-mods.zip
dllname = $(modname).dll

#
# actual build targets
#

default:
	mcs /target:library -r:../../../../colonyserver_Data/Managed/Assembly-CSharp.dll -r:../../Pipliz/APIProvider/APIProvider.dll -r:../../../../colonyserver_Data/Managed/UnityEngine.dll -out:"$(dllname)" -sdk:2 src/*.cs

clean:
	rm -f "$(dllname)" "modInfo.json"

enable:
	echo '{\n\t"assemblies" : [\n\t\t{\n\t\t\t"path" : "$(dllname)",\n\t\t\t"enabled" : true\n\t\t}\n\t]\n}' > modInfo.json

all: clean default enable

release: default
	rm -f "$(zipname)"
	cd ../../ && zip -r "$(moddir)/$(zipname)" "$(moddir)/modInfo.json" "$(moddir)/$(dllname)" "$(moddir)/capsule_permissions.json" "$(moddir)/assets/" "$(moddir)/blueprints/"

client: default enable
	cd ../../../../ && ./colonyclient.x86_64

server: default enable
	cd ../../../../ && ./colonyserver.x86_64

