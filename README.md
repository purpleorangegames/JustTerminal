# JustTerminal

A terminal inside the game, with an old CRT screen look. Type commands to see your colonists, factions and the colony.

Click the terminal button at the bottom bar to open it. Type help to see the commands, help and a command name shows examples for it. manual opens the manual about using it, manual programming the one about writing programs, both are in Common/Manual too.

You can keep notes in the terminal: mkdir, cd, ls, cat, cp, mv, rm and nano to write text files. They're kept in the save, not on your computer.

The folders under / are the game itself and can only be read: /bin, /colony, /pawns (a folder for each colonist), /health, /danger, /visitors, /idle, /power and /stock. Each file is one value, like cat /stock/food for the days of food left or ls /pawns/Tasca for all of Tasca's.

Files that end in .cron are cron scripts. They start with every (how often it checks, like 2h or 1d) or when (raid, downed, break, sick, death, letter or trader, it runs the moment that happens) and fire (each, daily or once), then if lines that all have to pass, then one action with the commands to run, ; between them. nano lowfood.cron starts one with an example. cron lists them, turns them on and off and changes every and fire, cron <script> test shows what each if reads now. cron_edit does the same with the keys. notify and letter show a message or a letter, for scripts. A script with a mistake shows as bad and can't be turned on, and one that breaks while running turns itself off and tells you why.

Files that end in .sh are programs, written in a small language: name = value, for x in /folder, if, else if, else, end, print, out, meter, run and stop. The commands that show something are programs you can read in /bin (fetch, colony, date, pawns, pawn, critical, checkup, visitors, danger, idle and factions), the edit commands work, medicine, area, policy, group and ungroup are programs too. The rest of /bin are the commands built into the mod. Programs change things with set, like set p/medicine = "herbal", only in edit mode and only on the files ls shows with can change. less reads a file without changing it. Programs can also write and append to files in ~ and ask the player something.

Pipes work: pawns | grep Sleeping, with grep, head, tail and wc. watch 5 danger runs danger every 5 seconds, alias gives a command line a short name. attention shows the alerts, untreated wounds and idle colonists. There's snake too. cp /bin/idle.sh ~/bin makes your own copy, the one in ~/bin runs instead, rm it to go back. Programs in ~/bin run by their name, others by their path. nano new.sh starts one with an example. The programs that come with the mod are in Common/Programs.

critical shows wounds nobody treated yet, checkup shows diseases with how the fight is going and the wounds already treated, visitors shows people from other factions on the map, danger shows enemies and wild predators, and idle shows colonists with nothing to do.

Click the little pin light on the top of the frame and it stays open when you click the map or open other tabs, and you can drag it around. Click it again to put it back on the bottom bar.

The normal commands only read, nothing you type changes the game. Type edit to go into edit mode, the screen turns red and the commands there change your colonists: work priorities, medicine, allowed area, policies, ideoligion roles (it opens the ritual for it), exile (the game asks first) and caravans (caravan alone lists the close towns, caravan and a town opens the caravan window with the route set). You can make groups of colonists and change all of them with one command, like group miners Drill Wolf Kovul and then work @miners mining 1. Type help in edit mode to see those commands, exit leaves it. You can keep your color in edit mode in the mod options.

rimtop (or top) is a colony monitor like btop: mood, temperature and wealth graphs, power and weather, stock, and the colonists listed like processes with their mood, health, rest, food and what they're doing. Up and down select a colonist, enter or a click shows their details (mood, health, skills, gear and work, left and right change the page), s changes the sort, r reverses it, tab switches between the four boxes and just one of them on the whole screen (or 1 to 4, 0 for all), q quits.

graph draws graphs over time: temperature outside (with a forecast), wealth, mood of the colony or of one colonist, food, colonists and raid points. Wealth, colonists and raid points come from the game's own history. For mood and food it keeps one value each hour in your save, so those start from when you added the mod. In the mod options you can turn that off so nothing from this mod goes in the save, and delete the recordings of the game you're playing. To remove the mod from a playthrough turn it off and save first.

It only takes the keyboard after you click on it, then the game's keys don't work until you click somewhere else. Esc closes it.

The mod options can export the files and programs of a game and import them into another one. Exports go in JustTerminal/Exports in the game's save folder, not the mod folder, so mod updates don't wipe them.

In the mod options (or type mod_options) you can make your own theme: colors for the text, the glass and the frame, make each effect stronger, weaker or turn it off, pick another font (an installed one or a .ttf/.otf you put in the fonts folder), the text size, the welcome text, commands to run when it starts (like fetch), the prompt, and give the commands your own names.

You can add it mid playthrough.

The font is DejaVu Sans Mono, its license is in Common/Fonts. The icon is an SVG in Art, Art/render.sh makes the PNG.

To build it run dotnet build Source -c Release.

To test it without playing, start the game with JTM_TEST_LOAD=<save name>, JTM_TEST_CMDS="help;pawns" and JTM_TEST_SHOTS=<folder>. It loads the save, opens the terminal, runs each command, takes a screenshot after each and quits. JTM_TEST_RUN=<frames> lets time run first and JTM_TEST_SAVEAS=<name> saves after that. write:<name>|<text> puts a file in ~ (\n for new lines, § for ;) and scripts runs the hourly script check right away.

CC0 for the code, you can do whatever you want with it, no need to give credits.

AI was used to help code/fix issues
