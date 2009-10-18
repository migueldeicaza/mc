SOURCES = \
	button.cs	\
	mc.cs		\
	panel.cs

mc.exe: $(SOURCES) Makefile
	gmcs -debug -out:mc.exe $(SOURCES) -pkg:mono-curses

run: mc.exe
	mono --debug mc.exe ; stty sane