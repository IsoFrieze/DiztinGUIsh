# HOWTO: Live tracelog capture with Diz+BSNES+

## Install Main Tools
1. Diz: https://github.com/Dotsarecool/DiztinGUIsh/releases/ (any version >= 2.1.X will be ok)
2. BSNES+ special version: https://github.com/binary1230/bsnes-plus/releases !!! NORMAL BSNES+ WONT WORK, YOU MUST USE THIS LINK !!!

## Start Capturing

### BSNES Setup
1. Open BSNES, Tools -> Debugger, check the 'Trace' box. 
2. BSNES will start listening on port 27015 and may appear to freeze (this is normal, though, crappy- sorry) until Diztinguish connects.

### Diz Setup
4. In Diztinguish, either open an existing project or import the same ROM file, and save your project before continuing.
5. In Diz, Click Tools -> Live Capture -> BSNESPlus Trace Logging
6. In the dialog that pops up, click connect and BSNES will unfreeze. It's working if the numbers in the GUI start updating.

BSNES will send data over socket connection to Diz, and you can save all that. Pretty fun.  Diz will mark M and A flags, and whether bytes are opcodes or instructions.

Note that the connecting/disconnecting process with BSNES is kind of janky right now, you may have to kill BSNES after you are done capturing, or to get it to listen 
on the socket properly again.

I recommend saving after every capture, and stopping and starting for longer playthroughs, just to be safe.

## Optional extra tools
1. If you have errors with assembling the exported assembly code from Diz with Asar, there's a fix for Asar for relative addressing on branches that's needed for some ROMS. [asar-domfix--github-issue-170--05-06-2021.zip](https://github.com/Dotsarecool/DiztinGUIsh/files/6432707/asar-domfix--github-issue-170--05-06-2021.zip)

## Visualizer

Once you have the other stuff working smoothly, there's an optional visualization mode that shows the live capture.

In DiztinGUIsh, before you open the capture dialog, click Tools -> Visual Map. Leave that up while you run the tracelog and watch your ROM get filled in.

This is a sped up video of about a 1 minute tracelog capture run (full version is here: https://www.youtube.com/watch?v=NCZUESf82Rg&feature=youtu.be)

![ezgif com-gif-maker](https://user-images.githubusercontent.com/5413064/97286056-69033900-1819-11eb-925d-67e1bbce95a7.gif)

# Extra tech info

## Notes

This has only been tested on localhost, I don't recommend you run it on a real ethernet network, it's not tuned for that, MTU might be crazy (would be kinda fun to see if it works)

Tip: If things are running too slow, in BSNESplus click the 'trace mask' button, which will filter areas that were already visited.

Please make a backup of your file if you have done significant disassembly. It should be pretty safe but, you never know.

For max fun results, export the disassembly before running the tracelog, then commit the export into a git repository.  Then, run the tracelog, and view the diff.  
You should hopefully see lots of the game code being revealed. Pretty Great!

Currently we only support 65816 importing, but, in the future might be able to add support for SPC, SA-1, etc.

## More stuff to try

Try also using BSNES-plus's 'usage map' feature, which is complementary to capturing. It can mark ROM sections are read, written, or executed.

In https://bsnes.revenant1.net/documentation.html look up the section about "Cache memory usage table to disk"
Diztinguish can take this file (a .bin file) and import it via File -> Import -> BSNES Usage Map

## Bonus tip: how to share / merge / diff Diz project files via git

By default, Diz will output a ```.diz``` file, which under the hood is an ```.xml.gz``` file.  That's fine, but if you are trying to collaborate on a disasembly project with other folks, it is helpful to work without the compression in the underlying XML-based format.  Diz's XML format was explicitly designed for ease of merging/diff while still maintaining a compact file size.

To work this way, simply save your project in Diz with the extension ```.dizraw```, and it will write your project in plain text/XML automatically.

You can check that project file into a git repository and collaborate with others.

# thanks 
@gocha for the initial work on this awesome trace system! @dotsarecool for the awesome tool

Any questions, come to the #diztinguish channel in the SNESLab Discord,
or also the Retro Game Mechanics Explained Discord https://discord.me/rgmechex, in the #your-tech-projects channel.
