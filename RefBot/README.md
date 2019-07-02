An implementation of SeeBorg for discord, with random features bolted onto it

Build it w/ visual studio or using msbuild DiscordDSPTestConnect.sln  
MSBuild is downloaded with the .NET framework and can generally be found in C:/Windows/Microsoft.NET/Framework/v.\<version>/ 

Requires the .Net framework to build/run and several configuration files (in the same dir as exe) to run:  
&nbsp;&nbsp;configs.dat:      Holds your bot token. YOU MUST SUPPLY THIS VALUE  
&nbsp;&nbsp;lines.txt:        Chatbot's persistent memory. Will be generated if absent.  
&nbsp;&nbsp;ChannelInfo.tdf:  Borg's channel information. Will be generated if absent.  
&nbsp;&nbsp;whitelist.txt:    Admin list of Discord IDs. Will be generated if absent.  

configs.dat contains configurations in the format \<KEY>:\<VALUE>; it REQUIRES a line like:

Bot-Token:\<HEY PUT YOUR BOT TOKEN HERE DUMBO>

-where the key is case sensitive and "\<HEY PUT YOUR BOT TOKEN HERE DUMBO>" is replaced by your bot token, generated from your account at discordapp.com.
