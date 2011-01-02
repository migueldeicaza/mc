SOURCES = \
	button.cs	\
	debug.cs	\
	mc.cs		\
	panel.cs	\
	util.cs

mc.exe: $(SOURCES) Makefile
	gmcs -debug -out:mc.exe $(SOURCES) -pkg:mono-curses -r:Mono.Posix

run: mc.exe
	mono --debug mc.exe ; stty sane