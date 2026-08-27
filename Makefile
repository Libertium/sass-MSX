# Thin wrapper around the .NET SDK, for those who prefer `make`.
# This branch targets .NET 10; there is no Mono/xbuild step any more.

CONFIG  ?= Release
DESTDIR ?= /usr/local
OUTDIR   = sass/bin/$(CONFIG)

all: build

build:
	dotnet build -c $(CONFIG)

clean:
	rm -rf sass/bin sass/obj

# Publishes a self-contained-of-the-framework folder and drops a `sasSX`
# launcher on the PATH that points at it.
install: build
	dotnet publish sass/sasSX.csproj -c $(CONFIG) -o $(DESTDIR)/lib/sasSX
	install -d $(DESTDIR)/bin
	printf '#!/bin/sh\nexec "$(DESTDIR)/lib/sasSX/sasSX" "$$@"\n' > $(DESTDIR)/bin/sasSX
	chmod +x $(DESTDIR)/bin/sasSX

uninstall:
	rm -f $(DESTDIR)/bin/sasSX
	rm -rf $(DESTDIR)/lib/sasSX

.PHONY: all build clean install uninstall
