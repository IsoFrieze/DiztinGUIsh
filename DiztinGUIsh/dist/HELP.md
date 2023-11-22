DiztinGUIsh Introduction
------------------------

DiztinGUIsh is a Super NES ROM disassembler. Open a ROM file, flag each byte as code or data, add some extra info like labels and comments, 
and disassemble. The output will be one or more .asm files that are (idealy) equivalent to the original source code of the game. 
The assembly files can then be re-assembled with [Asar](https://github.com/RPGHacker/asar/releases) to check for integrity,
and/or to make simple changes to the code.

As you may guess, tagging every single byte as data or code is a tedious process, so DiztinGUIsh has many tools built in to make the 
process more bearable. Disassembling 65C816 code can be quite tricky, so some human input is still required to get a 100% accurate disassembly, but most of the job will be done automatically.

Diz works GREAT with Tracelog tools, like BSNES+. We modified a version of BSNES to also support live capture from a running BSNES,
which means you can run the game at full framerate and grab all the juicy data coming off the CPU. This will mark millions of instructions
correct with the right flags for you, and is a fantastic way to crack a ROM's secrets wide open.

Be sure to also check out BSNES's usage map importing features to fill in sections of data vs code.

* * *

DiztinGUIsh Main Window
-----------------------

This is the window where most of the work is done. It consists of one very large table, some text at the bottom, and some options at the top.

#### File

Create new projects, open & save [projects](#projectfiles), [export a disassembly](#disassembly), and exit from this menu.

#### Tools

This menu will bring up additional tools to help you with managing data.

*   **Visual Map**: This window lets you see a visual map of the entire ROM, including what each byte is flagged as.
*   **Constants**: This changes what base the "raw byte" column is shown in. You can pick from decimal, hexadecimal, and binary.
*   **Options**
    *   **Move With Step**: When enabled, the currently selected cell in the table will jump to the byte after the last modified byte after a auto step or mark many operation.

* * *

### Project Files

All of the flags, labels, comments, and other data you make to help with disassembling will be put into a single [Project File](#fileformat) (\*.diz). 
This file contains a _link_ to the ROM you are currently working on, but doesn't contain the ROM itself. This is for copyright reasons, and so you can
easily share project files without worry of sharing ROM files as well. Because of this, if the ROM file you are working on is deleted or is relocated on 
your computer, DiztinGUIsh will ask to relocate the file. The ROM's internal name and checksum are stored to make sure you relink the same ROM you started 
with, but it is technically possible to relink a different ROM. Not sure what would happen then, but it wouldn't be anything you'd want.

When you start DiztinGUIsh, you can start a new project or open an existing one. Opening an existing project is simple, though you may have to relink 
your ROM if it moved, or if you received this project file from a friend. When you start a new project, DiztinGUIsh will first prompt you to select a 
ROM file you want to work on.

#### Starting a Project

DiztinGUIsh currently supports seven types of _ROM Mapping Modes_: LoROM, HiROM, two SA-1 modes, SuperFX, ExHiROM, & ExLoROM. The program will try its best 
to detect what mode the ROM you selected uses. There is a chance it won't be able to figure it out, or it will guess wrong. After you select a ROM file,
DiztinGUIsh will show the ROM's internal name. If this doesn't look correct, try changing the ROM Map mode.

_Note: Some Japanese titles will display as gibberish, so it is possible that the correct ROM Mapping Mode will result in a garbage looking title._

_Note: Although mapping mode 2, Super MMC, is an option, it currently functions identically to HiROM._

DiztinGUIsh also provides a few extra jumpstart options on the New Project window. 
The table of numbers on the bottom half of the window show the ROM's _vectors_, which are special pointers that locate code that runs under special 
circumstances (interrupts, bootup, etc.). By default, DiztinGUIsh will check off the vectors it thinks are important. Checking off a vector will 
automatically generate a label at the location of the pointer (e.g. "Native\_NMI").

If the final checkbox is checked, DiztinGUIsh will automatically mark flags for the entire internal header. This includes the internal name, 
ROM meta info & mapping settings, developer codes, & the vectors.

* * *

### Table Grid View

In DiztinGUIsh, _one byte equals one row in the table_. Instructions will be one or more rows depending on how many operands there are. 
Pointers will be more than one row. Each byte has many pieces of data associated with it--these are stored in each of the table's columns.

#### Label

Here you can create a custom label for this location in the ROM. Labels are useful to identify what things are in the ROM. 
You can label code, data, or anything really. Labels will be automatically used in the disassembly when available.

#### PC

This column shows the "program counter" for the current byte. This is effectively the location in the SNES's address space where this byte will be found.

#### @

This column shows an ASCII representation of the current byte. Mainly useful for locating text within the ROM.

#### #

This column shows the raw byte. You can change what base this value is displayed in via the View -> Constants menu.

#### <\*>

This column shows _in points, out points, end points, & read points_.

*   In points are locations in ROM where execution can jump to from somewhere other than the instruction directly before it. These are denoted by a ">" symbol. 
    In points are usually the result of a branch, jump, or call instruction.
*   Out points are locations where execution can jump elsewhere other than the instruction directly after it. These are denoted by a "<" symbol. 
    Out points are usually the result of a branch, jump, call, or return instruction.
*   End points are special out points where execution cannot directly flow from this instruction to the next directly after. 
    These are denoted by a "X" symbol. End points are usually the result of a jump or return instruction.
*   Finally, read points are locations in ROM that are the intermediate address of some other load/store/math instruction in the ROM. 
    These are denoted by a "\*" symbol. Read points are often the start of a table of data.

#### Instruction

This column shows what instruction this byte would be disassembled to if it were treated as an opcode.

The instruction may be highlighted in yellow if DiztinGUIsh thinks the instruction is risky. That is, a rarely used instruction that more likely 
than not means that the code is desynched.

#### IA

This column shows the intermediate address of the instruction or pointer located at this byte. The intermediate address is basically the
"address of importance" of an instruction--what address is actually being read/stored/jumped to, etc. Often, the instruction operands only 
specify a small chunk of the intermediate address, and the rest of it has to be inferred by other registers such as the program counter, 
data bank register, or direct page register. This column does all the math for you and spits out the intermediate address of the instruction. 
Note that this is not to be confused with the _effective address_, which is the "final result" address after locating indirect operands and/or
adding index registers.

The intermediate address will be calculated assuming this byte is marked as an opcode--UNLESS it is marked as some sort of pointer. Then, 
the intermediate address will just be the value of the pointer.

_Note: If the byte is marked as a 16-bit pointer, the bank of the intermediate address will be derived from the Data Bank register (B)._

#### Flag

This column displays what kind of data this byte will be treated as. Your primary job with Diz is to  
correctly mark the ROM with these flags

*   **Unreached**: This is the default flag. Basically means unknown. Your goal in Diz is clear all (or enough) unreached sections
*   **Opcode**: This byte is the opcode of an instruction. Zero or more Operands may follow it.
*   **Operand**: This byte is an operand of an instruction. Which instruction? The one marked as an Opcode a byte or more before it.
*   **Data (8-bit)**: Generic 8-bit (1-byte) long data.
*   **Graphics**: Special 8-bit data specifically used as graphics. Isn't treated any differently than plain 8-bit data (_yet_).
*   **Music**: Special 8-bit data specifically used as music. Isn't treated any differently than plain 8-bit data (_yet_).
*   **Empty**: Special 8-bit data specifically used as empty filler. Isn't treated any differently than plain 8-bit data (_yet_).
*   **Data (16-bit)**: Generic 16-bit (2-byte) long data.
*   **Pointer (16-bit)**: The lower 16-bits of a pointer. The bank of the intermediate address is derived from the Data Bank register (B).
*   **Data (24-bit)**: Generic 24-bit (3-byte) long data.
*   **Pointer (24-bit)**: A full 24-bit pointer.
*   **Data (32-bit)**: Generic 32-bit (4-byte) long data.
*   **Pointer (32-bit)**: A full 24-bit pointer, followed by a filler byte. This is common so that pointers are separated by 4 bytes (a power of 2).
*   **Text**: Special 8-bit data specifically used as ASCII text. This will be disassembled into a human-readable string.

#### B

The value of the _Data Bank register_ during execution of this code. Some instructions derive their intermediate address using the value of the data bank register.
The data bank register is also used as the bank byte of a 16-bit pointer.

The value will be highlighted in yellow if the instruction at this location stores the data bank register (PHB). It will be highlighted in red if the
instruction writes to the data bank register (PLB : MVP : MVN). This way you can be on a lookout for changes to this register.

#### D

The value of the _Direct Page register_ during execution of this code. Some instructions derive their intermediate address using the value of the direct
page register.

The value will be highlighted in yellow if the instruction at this location stores the direct page register (PHD : TDC). It will be highlighted in red 
if the instruction writes to the direct page register (PLD : TCD). This way you can be on a lookout for changes to this register.

#### M

The current state of the M flag in the program status register (P). When the flag is cleared (m = 0), the accumulator A is 16 bits wide. 
When it is set (M = 1), the accumulator A is 8 bits wide. Some instructions interpret their operands differently according to the state of the M flag. 
If some code is desynched or doesn't make since, try toggling the M flag.

The value will be highlighted in yellow if the instruction at this location stores the program status register (PHP).
It will be highlighted in red if the instruction writes to the program status register and potentially modifies the M flag (PLP : REP : SEP). 
This way you can be on a lookout for changes to this flag.

#### X

The current state of the X flag in the program status register (P). When the flag is cleared (x = 0), the index registers X and Y are 16 bits wide.
When it is set (X = 1), the index registers X and Y are 8 bits wide. Some instructions interpret their operands differently according to the state 
of the X flag. If some code is desynched or doesn't make since, try toggling the X flag.

The value will be highlighted in yellow if the instruction at this location stores the program status register (PHP). It will be highlighted in red
if the instruction writes to the program status register and potentially modifies the X flag (PLP : REP : SEP). This way you can be on a lookout for
changes to this flag.

#### Comment

Here you can write notes about the project. These comments will be output into the disassembly, but _only on bytes that start a line_.
For example, a comment on a byte that is marked as an opcode will be written, but a comment on an operand byte will not be written.

#### Status Bar

The status bar at the bottom of the window will show the number of bytes reached, and the currently selected marking flag.

#### Navigation

Use the arrow keys to move to different cells. Page Up & Page Down will move 16 rows at a time, and Home & End will move 256 rows at a time.

Cells you can type in are highlighted in green.

Intermediate addresses of the currently selected cell will be highlighted in pink. So will instructions whose intermediate addresses are the 
currently selected cell.

* * *

### Stepping

**Hotkey: S, I**

Stepping is the method of marking bytes as code. Instead of marking each byte as opcode and operand separately, you can step through the instruction, 
and DiztinGUIsh will automatically assign opcode and operand flags as necessary.

Stepping an instruction will automatically copy the D, B, M, & X values from the previous instruction. If a REP or SEP instruction is stepped, 
the M and X values will be modified as necessary.

_Note: Most trace logs will reflect changes to the D, B, M, & X values on the instruction after the one that modifies the register. However, 
DiztinGUIsh likes to show the updated values on the same row as the instruction that updates the values._

There are two stepping commands: step, and step in. Step will advance the selector to the instruction directly following this instruction. 
Step in will advance to the intermediate address of this instruction. This is useful for taking a branch or jumping into a routine call. 
Note that plain step will still step in if the instruction is a jump instruction.

* * *

### Auto Stepping

**Hotkey: A, Ctrl + A**

There is a lot of code in a ROM, so stepping through each instruction can be time consuming. Therefore, this option will let you step through 
instructions automatically until something happens.

#### Auto Step (Safe)

This is the recommended way of auto stepping. When auto step hits a branch, it will not take it. When it hits a jump, it will jump. When it
hits a function call, it will save the program counter and jump to the routine. When it hits a return, it will try to restore the program
counter to what it was before the call. It also saves and restores the value of the program status register and the M and X flags.

Basically, it pretends to execute code until it hits something it can't do. Some jump instructions refer to values in RAM, which are not kept
track of. At this point, the auto step will pause and wait for you to continue it at a point that makes sense.

Auto Step will also stop if it hits a branch or jump that it has already seen in one go. This is to prevent it getting stuck on infinite loops.
It will also stop if it hits a risky instruction--that is, a rare instruction that more likely than not means that the code is desynched.
It will also stop if it hits an instruction that is already marked as something other than code.

#### Auto Step (Harsh)

This auto step will rush through bytes like a freight train and disassemble them one directly after the other. It doesn't care about jumps,
branches, or anything. The only smart thing it does is updates the M and X flags upon stepping through REP and SEP instructions.

Because of how hardy it is, it will never stop if it hits something risky. You have to specify how many bytes to disassemble. It will also
clobber over stuff that is already marked as something other than code.

* * *

### Goto

**Hotkey: Ctrl + G, T, U, H, N, F3**

This is an easy way to hop around the ROM without having to scroll all the way to where you want.

Hitting Ctrl + G will bring up the [Goto window](#gotowindow). You can type in a SNES address or ROM file offset in decimal or hex to go directly to that location.

You can press T to jump to the intermediate address of the currently selected instruction. Only if the intermediate address refers to ROM of course.

You can press U, H, or N to jump to the first unreached byte of the project, the nearest unreached block behind the selected address, or 
the nearest unreached block ahead of the selected address. These are useful just to get somewhere you haven't been yet.

Press F3 to go to the "Next Unreached In Point". Handy when working with tracelog imports, which fill in a LOT of opcodes but leave small branches unmarked 
that are rarely taken by normal gameplay.

The History window is a record of your recent activity in the project.  You probably want to be best friends with ALT+LEFT and ALT+RIGHT while stepping,
since branches can jump you all over the codebase and you can lose your train of thought.

* * *

### Marking

**Hotkey: K, Ctrl + K, B, D, M, X**

For anything that isn't code, you will have to mark manually. You can select which flag type to mark with under the Edit -> Select Marker menu. 
The currently selected flag type is shown in the status bar at the bottom of the window.

Pressing K will mark a single instance of that flag type. That is, 8-bit data will mark 1 byte at a time, 16-bit data 2 bytes at a time, etc.

You can press Ctrl + K to bring up a window that lets you mark an entire range at once. You can specify SNES address or ROM file offset, 
in hex or decimal, and you can specify the range via start & end points, or just the number of bytes to mark.

Pressing B, D, M, or X will jump the selected cell to the data bank, direct page, M flag, or X flag cell respectively. You can hold Ctrl while 
pressing one of these keys to bring up the same window as Ctrl + K with default options to mark that value.

* * *

### Labels and Comments

**Hotkey: L, C**

Pressing L will jump the selected cell to the label cell automatically so you can add a label to this byte. 
Pressing C will do the same for the comment column.

#### Valid Labels

Asar has a limitation on what characters can make up a label, and DiztinGUIsh follows the same format. Currently, 
labels must only contain the following characters:

a-z A-Z 0-9 \_

Characters within a label that don't fit this description will be converted to \_s.

DiztinGUIsh currently does not support sublabels (starting with a .), or +/- labels (made of \+ - characters).

DiztinGUIsh also currently does not check for duplicate labels; that is, two more more addresses with the same label. 
For now, please use the [Label List](#labellist) to check for any duplicate labels.

* * *

### Misalignments & Desynching

**Hotkey: Ctrl + F**

_Desynching_ refers to incorrect instructions being disassembled due to opcodes being marked as operands and vice versa. 
Some instructions have a different amount of operand bytes associated with them depending on the state of the M and X flags. 
If these flags are assumed incorrectly, the size of the instruction will be assumed incorrectly, causing a byte that should 
be an operand being treated as an opcode or vice versa.

Sometimes desynched instructions are hard to catch. In fact, they can be disassembled and reassembled, and you wouldn't even notice. 
The SNES CPU would just execute the code as it would normally; it just wouldn't match what the disassembly says. 
For example, take the following bytecode: A9 00 8D 85 00. This could be LDA #$00 : STA $0085 or it could be LDA #$8D00 : STA $00.
Both are perfectly legitimate, but only one is correct.

A common cause of desynched code in DiztinGUIsh has to do with stepping through code while the X and M flags are incorrect. 
Sometimes, bytes can be marked as opcodes and operands incorrectly that accidentally overwrite already marked bytes.

In DiztinGUIsh, _misalignment_ refers to bytes that are marked incorrectly, due to the length of the instruction or data not
matching the flags. For example, the TAX instruction has no operands. But if the byte following this instruction is marked as 
an operand, this is a misalignment. Misalignments can also occur with data and pointers. If there is a block of 9 bytes marked 
as 16-bit data, there is a misalignment somewhere since the number of bytes is odd.

The Misaligned Flags checker window will look for misalignments. You can just scan the ROM without actually modifying anything.
Misalignments will be identified and output into the text box so you can correct them manually. You can also just have DiztinGUIsh 
attempt to fix all misalignments automatically. It uses a pretty brute force method, so it may not produce correct results, 
but at least it will get rid of all misalignments.

It is recommended to scan for misalignments before outputting a disassembly.

* * *

### In/Out/End/Read Points

**Hotkey: Ctrl + P**

As DiztinGUIsh steps through instructions, it will mark [in points, out points, end points, and read points](#tablegrid).
However, marking bytes as opcode and operand manually will not produce these points. Importing CDLs will also not generate points. 
Also, once a byte is marked with a point, you can't unmark it.

The Rescan for In/Out Points window will clear all points and readd them using the current flags.

It is recommended to rescan for in/out/end/read points before outputting a disassembly.

* * *

### Keyboard Hotkeys

Pretty much everything has a hotkey associated with it, so you can use the entire program with the keyboard.
Note that hotkeys won't work when the selected cell is editable, as typing anything will just put it into the box.

* * *

Goto Window
-----------

Using this window you can select any byte in the ROM without having to scroll all the way to it.

* * *

### SNES Address vs ROM file Offset

The SNES address is the location in the SNES's address space where the byte is located.

The ROM File Offset (also called the PC offset) is the raw offset of the byte in the ROM file. 
The first byte has an offset of 0. The ROM is always treated as unheadered, even if the ROM linked to the project has an SMC header.

* * *

### Hexadecimal vs Decimal

Hexadecimal is a base 16 counting system. It is what most hex editors use for displaying offsets, since 16 is a power of 2.

Decimal is a base 10 counting system.

_Note: Using decimal to specify the SNES address is silly, I've found._

* * *

Mark Many Window
----------------

This window lets you set flags and registers for more than one byte at a time.

* * *

### Property & Value

You can choose which property to modify, and what value to give for the entire specified range.

The property field will default to a different setting depending on what hotkey you used to open the window.

* * *

### Address Range

See the [Goto Window](#gotowindow) for info on ROM vs PC and Hex vs Dec.

Changing the bounds of the range will automatically update the number of bytes and vice versa. If the values you input
go past the end of the ROM, they will snap to the end of the ROM.

* * *

Fix Misaligned Flags Window
---------------------------

See [Misalignments & Desynching](#desynching).

* * *

Rescan for In/Out Points Window
-------------------------------

See [In/Out/End/Read Points](#inoutpoints).

* * *

Label List Window
-----------------

This window shows every single label that has been created, and the address it is associated with. 
Labels in this list will automatically be updated, added, or removed when they are modified via the Main Window.
You can also add, remove, or edit them from this window. The labels can be sorted by address or name by clicking on the table header.

#### Jump to

Clicking on this button will jump to the location of this label in the Main Window.

#### Import...

Use this to import a \*.csv file with a list of labels in the format described below. 
This is useful if you have an external program that generates labels for you.

We also support importing from BSNES's symbol file format.

#### Export...

This will export a \*.csv file with all of the current labels. The format is quite simple; each label is one line in the CSV file, formatted as such:

snes\_address,label\_name

Note: Since neither the address nor the label should contain spaces or quotation marks, DiztinGUIsh will expect no quotation marks surrounding either field.

#### Label Table

Pretty self-explanitory--the first column is the SNES address that the label name in the second column corresponds to.

You can modify labels by double-clicking on them. Hitting the del key will delete a label. You can even select more than one at once to delete.
To add a label, just add the address and label name to the empty row at the end of the table. Make sure the address is a valid 24-bit address 
in hexadecimal, that the address you enter isn't already in the list, and that the label name contains only valid characters. 
See [here](labelcomment) for what makes a valid label name.

Since DiztinGUIsh currently does not prevent you from reusing a label name more than once, it will warn you of duplicates by 
highlighting the rows that contain duplicate label names in yellow.

* * *

Export Disassembly Window
-------------------------

This window lets you set the options for outputting a disassembly that can reassembled by Asar.

* * *

### Output Format

This box lets you choose how each line of the disassembly will be formatted. There are several types of arguments you can specify. Arguments are put inbetween percent signs (%). Some arguments can be followed by a length parameter, which is defined by a colon (:) and then a number. A positive number will align the argument to the left; negative will align to the right.

*   **\***: Anything that isn't an argument will be output directly into the line.
*   **%%**: Output a percent sign.
*   **%label:length%**: Output the label for this line. Default length of -22.
*   **%code:length%**: Output the instruction, data, pointer, etc. for this line. This is where assembler directives will appear as well. Default length of 37.
*   **%ia%**: Output the intermediate address of this instruction or pointer if applicable. Forced length of 6.
*   **%pc%**: Output the SNES address of this code. Forced length of 6.
*   **%offset%**: Output the ROM file offset of this code. Forced length of -6.
*   **%bytes%**: Output the bytecode of this instruction. Forced length of 8.
*   **%comment:length%**: Output the comment for this line.
*   **%b%**: Output the Data Bank register for this line. Forced length of 2.
*   **%d%**: Output the Direct Page register for this line. Forced length of 4.
*   **%m:length%**: Output the M flag for this line. Default length of 1. If length is 1, it will output "M" when set and "m" when cleared. Otherwise, it will output "08" or "16".
*   **%x:length%**: Output the X flag for this line. Default length of 1. If length is 1, it will output "X" when set and "x" when cleared. Otherwise, it will output "08" or "16".

You can also check the box for generating all labels. This will print a labels.csv and all-labels.txt which will show all labels regardless
of whether they are used in the final assembly (handy if your labels are for memory addresses that are not directly referenced in the code)

* * *

### Unlabeled Instructions

This controls how DiztinGUIsh will output lines that have no labels.

*   **Create All**: Temporary labels will be added to every line.
*   **In Points Only**: Temporary labels will only be added to in points and read points.
*   **None**: No extra labels will be added.

* * *

### Bank Structure

This controls the structure of the output disassembly.

*   **All in one file**: The disassembly will be contained within one, potentially large file.
*   **One bank per file**: Each bank will be put in a separate bank\_\*.asm file. A file called main.asm will be created as the root file, 
     and a file called labels.asm will be created to store all label assignments (e.g. RAM addresses).

* * *

### Max Data Bytes Per Line

This controls the maximum number of bytes output per line on bytes marked as data.

This is actually not the max, but the "min-max"--a maximum of 5 bytes will still allow two 32-bit data elements (total of 8 bytes) per line.
A maximum of 4 bytes will limit this to one 32-bit data element (4 bytes). In other words, a data element will not be split over two lines.

* * *

### Reassembly with Asar

If DiztinGUIsh outputs a disassembly without any warnings, it should _always_ be assemblable with Asar. 
The code may be desynched or flagged incorrectly, but it should still assemble at least. You can assemble a 
disassembly via the command asar.exe \[name\].asm \[output\].sfc, where \[name\] is the disassembly file
(or main.asm if output to multiple files).

In theory if the world were perfect, the reassembled ROM should match the original ROM. Sometimes it will, 
and sometimes it won't. It depends on the code itself. The output should always _run_ similarly 
(unless the ROM has built-in modification or checksum detection).

The reason this may not be the case is due to mirroring, and the way DiztinGUIsh deals with labels. 
Take the following two code snippets: JML $C00054 : JML $400054. One has an intermediate address of $C00054 
and the other has intermediate address $400054. These two addresses refer to the same byte in the ROM due to
memory mapping mirroring. That is, they both refer to PC offset 0x54. DiztinGUIsh stores labels according to SNES addresses.

* * *

Project File Format
-------------------

The default file format is .diz, which is XML compressed with gzip. (you can rename the file to .xml.gz)

You can also save with a .dizraw extension, which is the same thing but without the compression.

Choose this option if you want to easily modify project files in an external program, or for use with git where
you want to see the diffs.  The underlying format is XML.

* * *
