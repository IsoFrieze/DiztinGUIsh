this temporary documentation describes how to install a custom patch of BSNES-plus to work with Diztinguish for capture logging.
To use this to do live tracelog capture from BSNES-plus:

# Install
1. Get latest pre-release zip of DiztinGUIsh from https://github.com/binary1230/DiztinGUIsh/releases (anything newer than v2.0-beta6 is OK)
2. Download a special patched version of BSNES-Plus from here: https://github.com/binary1230/bsnes-plus/releases (you must use this fork, the upstream version doesn't support capture yet)

# Run
1. Open BSNES, Tools -> Debugger, check the 'Trace' box. 
2. BSNES will start listening on port 27015 and appear to freeze (this is normal, though, crappy) until Diztinguish connects.
2. In Diztinguish, import your ROM file first, save the project.
3. Click Tools -> Live Capture -> BSNESPlus Trace Logging
4. In the dialog that pops up, click connect and BSNES will unfreeze

BSNES will send data over socket connection to Diz, and you can save all that. Pretty fun.  Diz will mark M and A flags, and whether bytes are opcodes or instructions.

# Extra

Tip: If things are running too slow, in BSNESplus click the 'trace mask' button, which will filter areas that were already visited.

Please make a backup of your file if you have done significant disassembly. It should be pretty safe but, you never know.

For max fun results, export the disassembly before running the tracelog, then commit the export into a git repository.  Then, run the tracelog, and view the diff.  
You should hopefully see lots of the game code being revealed. Pretty Great!

Currently we only support 65816 importing, but, in the future might be able to add support for SPC, SA-1, etc.

# visuals

totally optional but fun: In DiztinGUIsh, before you open the capture dialog, click Tools -> Visual Map. Leave that up while you run the tracelog and watch your ROM get filled in.

This is a sped up video of about a 1 minute tracelog capture run (full version is here: https://www.youtube.com/watch?v=NCZUESf82Rg&feature=youtu.be)

![ezgif com-gif-maker](https://user-images.githubusercontent.com/5413064/97286056-69033900-1819-11eb-925d-67e1bbce95a7.gif)

# further reading

Try also using BSNES-plus's 'usage map' feature, which is complementary to capturing. It can mark ROM sections are read, written, or executed.

In https://bsnes.revenant1.net/documentation.html look up the section about "Cache memory usage table to disk"
Diztinguish can take this file (a .bin file) and import it via File -> Import -> BSNES Usage Map

# thanks 
@gocha for the initial work on this awesome trace system! @dotsarecool for the awesome tool

Any questions, come to the RGMX Discord https://discord.me/rgmechex, in the #your-tech-projects channel.
