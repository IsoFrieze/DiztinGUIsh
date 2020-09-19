# DiztinGUIsh-FORK1

Note from fork author binary1230:
This is a fork of DiztinGUIsh https://github.com/Dotsarecool/DiztinGUIsh which adds some bells and whistles like:
- UX improvements (progress bars, easier GoTo search, open last project automatically, etc)
- Attach comments to labels themselves for documentation
- Integration and extension of functionality from @Gocha's fork: https://github.com/gocha/DiztinGUIsh for support for BSNESplus trace logs and usage maps. HIGHLY RECOMMEND if you're doing any serious disassembly work, it's super-useful.
- Various other small fixes/improvements

Works fine with stock asar though, there's a bugfix you may want:
- https://github.com/binary1230/asar/tree/fix_relative_addressing/src/asar

See discussion about the future of this fork here:
- https://github.com/Dotsarecool/DiztinGUIsh/issues/16
- I hope to include all of the changes from this fork back upstream, but, maintaining this fork for the moment.

# DiztinGUIsh original documentation follows

A Super NES ROM Disassembler.

"Distinguish" but with a 'z' because it's rad. It's also a GUI application so might as well highlight that fact.

## Doesn't this already exist?

There is at least one 65C816 disassembler out there already. The biggest issue with it (not with that program, but with disassembling 65C816 in general) is that some instructions assemble to different sizes depending on context. This makes it difficult to automate. 

A ROM contains two broad categories of stuff in it: code and data. A perfect disassembler would isolate the code, disassemble it, and leave the data as it is (or maybe neatly format it). Differentiating data from code is already kinda hard, especially if the size of the data isn't explicitly stated. A perfect program would need context to do its job. Turns out that keeping track of all memory and providing context for these situations is pretty much emulation. Some emulators have code/data loggers (CDLs) that mark every executed byte as an instruction and every read byte as data for the purpose of disassembly. A naive approach to disassembling then, would be to disassemble *everything* as code, then leave it up to a person to go back and mark the data manually. Disassembling code is the most tedius part, so this isn't a bad approach.

In the 65C816 instruction set, several instructions assemble to different lengths depending on whether or not a bit is currently set or reset in the processor flag P register. For example, the sequence `C9 00 F0 48` could be `CMP.W #$F000 : PHA` or `CMP.B #$00 : BEQ +72` depending on if the accumulator size flag M is 0 or 1. You could guess, but if you're wrong, the next however many instructions may be incorrect due to treating operands (`#$F0`) as opcodes (`BEQ`). This is known as *desynching*. So now you need context just to be able to disassemble code too.

Now for the most part, you can get away with just disassembling instructions as you hit them, following jumps and branches, and only keeping track of the M and X flags to make sure the special instructions are disassmbled properly. But more likely than not there will be some jump instructions that depend on values in RAM. Keeping track of all RAM just to get those effective addresses would be silly--again, it would basically be emulation at that point. You'll need to manually determine the set of jumps possible, and start new disassmble points from each of those values. Don't forget to carry over those M and X flags!

Things get more complicated if you want to determine the effective address of an instruction. Instructions like `LDA.L $038CDA,X` have the effective address right in the instruction (`$038CDA`). But most instructions look something like `STA.B $03`. The full effective address needs to be deduced from the data bank and direct page registers. Better keep track of those too!

So to take all of this into consideration, DiztinGUIsh tries to make the manual parts of disassembling accurately as speedy as possible, while still automating the easy parts. The goal of this software is to produce an accurate, clean disassembly of SNES games in a timely manner. Any time things look dicey, or if it becomes impossible to disassemble something accurately, the program will pause and wait for more input. Of course, there are options to go ham and just ignore warnings, but proceed at your own risk!

## Features

I'm working on porting this over from a scrappy Java app I made a long time ago. There are also some things I want to add.

Implemented or currently in progress:

* Manual and Auto Stepping
* Stepping into a branch or call
* Goto effective address
* Goto first or nearby unreached data
* Marking data types (plain data, graphics, pointers, etc.)
* Tracking M & X flags, Data Bank & Direct Page registers
* Saving/Loading project files that store all of the above data
* Producing a customizable output file that assembles with asar
* Add custom labels and comments that fit into the disassembly file

Planned stuff:

* SPC700 & SuperFX architechtures
* Importing CDL files from emulators
* Importing trace logs from emulators
* Merging multiple project files together
* Labelling effective addresses that aren't ROM
* Visual map of the entire ROM; seeing what is/isn't disassembled
* Programmable data viewer to locate graphics easily
* Setting a "base" per instruction for relocateable code
* Option to put large data blocks into separate .bin files intead of in the .asm
* Scripting engine & API
