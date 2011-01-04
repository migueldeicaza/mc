SOURCES = \
	button.cs	\
	debug.cs	\
	fileops.cs	\
	mc.cs		\
	panel.cs	\
	util.cs

mc.exe: $(SOURCES) Makefile
	dmcs -debug -out:mc.exe $(SOURCES) -pkg:mono-curses -r:Mono.Posix

run: mc.exe
	mono --debug mc.exe ; stty sane