Started as a DIY to apply the obd unlock to the Gen1 and select Gen2 BMW iRoms instead of paying another company between 2-300.00 for something that takes less than 30 minutes. Initially created by O.S. Automotives, I have added the ability to read and display the current PRG (for using tunerpro), Identifies the engine code, Chassis, and displays the generation. it works for all Gen1/2 F&G series up until June 2020.

**>>>> THIS WILL NOT READ THE DME, YOU MUST ALREADY HAVE A BENCH READ TOOL THAT CAN READ AND WRITE THE iRom/BIN FILE TO THE DME<<<<**

To use, download the python script and run from your desktop, once the program opens, select "load bin"-->"patch bin"-->"save bin as". write modified bin back to the DME. it si also packeaged into a .exe file so you can run it straight from your desktop without the need to install python.

**------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------**

                   ** Use the information below to prepare the ORI/iROM to be used with TunerPro:**

Use the tool to see your current PRG, go here and download the xdf and stock bin file (generating the stock bin file in MHD is fine as well) https://github.com/dmacpro91/BMW-XDFs/tree/master/B58gen1

for Gen 1 open the stock bin with HxD and go to address 0xD00, select everything from that address to 0x000 (ctrl+e, change "start offset" to 000 and hit enter) then right click and copy (ctrl+c), open the tune you pulled off the DME, and go to address 0x40D00 and select everything from that address to 0x000, then right click and paste the data you saved from the stock bin file, it will warn about changing file size, just select ok. save your file and open with TunerPro making sure to use the right xdf file. 

If you want to use your bench flash tool to write back to the DME instead of using MHD, then first copy the data from the tuned file (0x000-0x40D00) and just open a new file in HxD and paste it there to reference later. when you are ready to bench flash it back to the DME after modifying the tune, copy the small bit of data you saved earlier insert/and overwrite the data between 0x000 and 0xD00, select ok to changinf file size, then save tune. MHD and the bench flash tool will correct the checksum for you.

Gen 2 will open correctly in TunerPro with the right xdf, no need to modify the bin, but if you run into any issues, go to address 0x280ff select all to 0x000 using ctrl+e, right click the highlighted hex and select "fill selection" it should already have 00 in fill pattern and you can just hit enter, then save the file.
