# DiztinGUIsh ("Diz")
[![Build Status](https://github.com/Dotsarecool/DiztinGUIsh/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Dotsarecool/DiztinGUIsh/actions/workflows/dotnet.yml)

A Super NES ROM Disassembler and tracelog capture/analysis tool with a focus on collaborative workflow UX. Exports .asm files ready to be compiled back into the original binary. Written in Winforms/C#.

_Diz tools suite:_
![image](https://user-images.githubusercontent.com/5413064/110195709-45767d80-7e0d-11eb-9f5f-1e21489dc8cd.png)

Official support channel is #diztinguish in the https://sneslab.net/ discord

---

# Features

## Main features

**Disassembling programs** (like SNES games) for some CPU architectures (like the SNES's 658016) is a pain because you have to know a lot of information about the program at the point where it's running. Diz is designed to make this less of a nightmare.

_Demo of basic disassembling:_
![ezgif com-gif-maker](https://i.imgur.com/Tb2H484.gif)

---

**Realtime tracelog capturing**: We provide a tight integration with a custom BSNES build to capture CPU tracelog data over a socket connection. You don't have to play the game at 2FPS anymore, or deal with wrangling gigabyte-sized tracelog files.  Simply hit 'capture' and Diz will talk directly to a running BSNES CPU, capturing data for as long as you like. Turn the ROM visualizer on and watch this process in realtime.

![ezgif com-gif-maker](https://user-images.githubusercontent.com/5413064/97286056-69033900-1819-11eb-925d-67e1bbce95a7.gif)
![image](https://user-images.githubusercontent.com/5413064/97133932-ed729080-1721-11eb-894e-4c110787aa75.png)

For more details, visit the [Tracelog capturing tutorial](https://github.com/Dotsarecool/DiztinGUIsh/blob/master/TRACE%20CAPTURE%20INSTRUCTIONS.md)

## Other useful features

- Tracelog file import support for Bizhawk and BSNES (record where the CPU is executing and what flags are set)
- BSNES usage map import / Bizhawk CDL import (record which sections of ROM are code vs data)
- Annotation of ROM and RAM addresses, labels, and comments. These are exported in the assembly output for humans
- Merge-friendly XML based file format. Save your project file with a .dizraw extension (~1.5MB), and the uncompressed XML is easy to share, collaborate, and merge with other people easily.  Great for group aggregration projects or building a database from various sources of info laying around the internet. Re-export the assembly and generate code with everyone's collective efforts stored in one place. Say goodbye to search+replace for adding labels and variable names all over the place.
- ROM visualizer, view which parts of the ROM you've marked as code vs data, and see visual progress.
- C# .NET WinForms app, easy to add features to. Write your own plugins or use our plumbing or GUI as a base for your own tools.

NOTE: Works fine with stock asar though, there's a bugfix you may want:
- https://github.com/binary1230/asar/tree/fix_relative_addressing/src/asar

## Details

### Doesn't this already exist?

There is at least one 65C816 disassembler out there already. The biggest issue with it (not with that program, but with disassembling 65C816 in general) is that some instructions assemble to different sizes depending on context. This makes it difficult to automate. 

A ROM contains two broad categories of stuff in it: code and data. A perfect disassembler would isolate the code, disassemble it, and leave the data as it is (or maybe neatly format it). Differentiating data from code is already kinda hard, especially if the size of the data isn't explicitly stated. A perfect program would need context to do its job. Turns out that keeping track of all memory and providing context for these situations is pretty much emulation. Some emulators have code/data loggers (CDLs) that mark every executed byte as an instruction and every read byte as data for the purpose of disassembly. A naive approach to disassembling then, would be to disassemble *everything* as code, then leave it up to a person to go back and mark the data manually. Disassembling code is the most tedius part, so this isn't a bad approach.

In the 65C816 instruction set, several instructions assemble to different lengths depending on whether or not a bit is currently set or reset in the processor flag P register. For example, the sequence `C9 00 F0 48` could be `CMP.W #$F000 : PHA` or `CMP.B #$00 : BEQ +72` depending on if the accumulator size flag M is 0 or 1. You could guess, but if you're wrong, the next however many instructions may be incorrect due to treating operands (`#$F0`) as opcodes (`BEQ`). This is known as *desynching*. So now you need context just to be able to disassemble code too.

Now for the most part, you can get away with just disassembling instructions as you hit them, following jumps and branches, and only keeping track of the M and X flags to make sure the special instructions are disassmbled properly. But more likely than not there will be some jump instructions that depend on values in RAM. Keeping track of all RAM just to get those effective addresses would be silly--again, it would basically be emulation at that point. You'll need to manually determine the set of jumps possible, and start new disassmble points from each of those values. Don't forget to carry over those M and X flags!

Things get more complicated if you want to determine the effective address of an instruction. Instructions like `LDA.L $038CDA,X` have the effective address right in the instruction (`$038CDA`). But most instructions look something like `STA.B $03`. The full effective address needs to be deduced from the data bank and direct page registers. Better keep track of those too!

So to take all of this into consideration, DiztinGUIsh tries to make the manual parts of disassembling accurately as speedy as possible, while still automating the easy parts. The goal of this software is to produce an accurate, clean disassembly of SNES games in a timely manner. Any time things look dicey, or if it becomes impossible to disassemble something accurately, the program will pause and wait for more input. Of course, there are options to go ham and just ignore warnings, but proceed at your own risk!

## Features

Implemented or currently in progress:

* Manual and Auto Stepping
* Stepping into a branch or call
* Goto effective address
* Goto first or nearby unreached data
* Marking data types (plain data, graphics, pointers, etc.)
* Tracking M & X flags, Data Bank & Direct Page registers
* Producing a customizable output file that assembles with asar

Planned stuff:

* SPC700 & SuperFX architechtures
* Merging multiple project files together
* Better labelling effective addresses that aren't ROM
* Programmable data viewer to locate graphics easily
* Setting a "base" per instruction for relocateable code
* Option to put large data blocks into separate .bin files intead of in the .asm
* Scripting engine & API


### "Distinguish" but with a 'z' because it's rad. It's also a GUI application so might as well highlight that fact."
