# FriendTorrent
 

### Tasks:
	* [Done] Serialization/Desirialization BEencode library 
	* [Done] Parse peers list from tracker and connect to peers
	* [Done] Work on message chunks for peers (Sending and Receiving packets)
	* [Done] Handle all Message types in Downloading Peer
	* [Done] Implement peer seeding
	* [Done] For multithreading instead of spinning a thread for each peer. Use async Tasks to download chunks, we can use logic where we download as many pieces available from a peer and then move on to others that more available chunks for us. 
	* [Done] Add in-progress torrents that have exhausted their peer list to a seperate queue/list (await next tracker update)
	* [Done] When a peer is no longer in the tracker remove them from the tracker list
	* [In progress] Code cleanup.
	* [Todo] Add a last interested to seeding task
	* [Todo] Command arguments, reconfigure where resources are added/downloaded
	* [In progress] Change ynknown packet type behavior
		* Instead of killing the connection, flush our socket and 'reset' our interaction (keep bitfield + interested flags, call SendPieceRequest agaiin)
	* [In progress] Inncrease speed of retrying connections with peers

### Bugs
	* The process cannot access the file 'C:\Users\asdfj\source\repos\FriendTorrent\BTProtocol\BTProtocol\Resources\TorrentData\[SubsPlease] Shiro Seijo to Kuro Bokushi - 09 (1080p) [3F93AFA7].mkv' because it is being used by another process (Went away???)


### Console Application Planning
	* Add flags
		- Debug
		- Mode [Seeding,Downloading,All]
		- Path
	* BTProtocol.exe [Console Application Options]
		* Add TorrentFile
			- Ex. > C:\Downloads|	
			- Adds torrent file from path given to path specified at the start of the executable 
		* Available Torrents
		* Queue Torrents for Install [Default = All]
			* exarm
			* centos
			* myreallynamefile.txt
			* All
		* Start
		* Paused/Stopped
		* Exit

	* Work Needed
		- Design console code
			- Design coommand args
		- Add Interrupts
			- Stopping specific torrents
		- Redesign how 'main' function interacts with other classes
			- Ex. Downloading Queue popualted by 'main' class / console
		- On application startup, confirm that torrents / downloaded data is accurate
		

### UI Plannning:
	* ???????
	

### Todo:
	 * Nice to have: 
		* Verify that files/pieces downloaded are correct through a hash comparison when starting the program
			* Manual hash calculation + check serialized file
		* Todo notify others Cancel to if they are downloading the same piece (useful in 'endgame')
		
		* Integration with friend viewer co. ping Shio

	 * Wistful Thinking:
		* uTP protocol
		* We are currently unable to download 2 different torrents from the same peer/port combination
			- One of the torrent tasks will not be able to download any pieces (possibly because the other task is consuming all of the incomining data)


### Logic/Ideas
	* Create up to N threads for connections to peers. 
		** 1 Thread will be used to upload pieces to connected peers
		** 1 Thread will be used to update all the trackers for the torrents we own
		** Remaining Threads will download missing torrent pieces
	* Task/Logic:
		** Each thread will start by selecting a piece to download.
		** It will go through the list of unconnected peers until it finds one who has the piece it is looking for
		** It will then download that piece from the selected peer
		** When selecting the next piece to download, the thread will prioritize pieces that its current peer has available


#### Useful Links:
	* http://www.bittorrent.org/beps/bep_0015.html
	* https://www.bittorrent.org/beps/bep_0003.html (Actual documentation)
	* https://wiki.theory.org/BitTorrentSpecification
	* https://www.seanjoflynn.com/research/bittorrent.html#trackers
	* http://www.kristenwidman.com/blog/33/how-to-write-a-bittorrent-client-part-1/




### Pseudo Documentation
Order of piece and block downloads:
	- Pieces downloads are random, each task is randomly assigned a piece to download from their connected peer (among the list of pieces the peer owns)
		- This is to avoid collision between different tasks trying to download the same piece, and to prevent slowdowns from downloading the torrent sequentially
	- Blocks are downloaded sequentially for each piece (Potentially will change)
		- Tasks will download each piece starting from the first block, and will request the next block only after recieving the block it request
		- Todo: UDP (Resends)

Tracker Peculiarities:
	- We do not implement a backoff for UDP trackers. Once we recieve an error from a tracker (timeout, incorrect response, etc) the tracker is dropped and we do not contact it again
	- Proper behavior is to use a backoff of 15 * 2^n, where n starts at 1
	- Trackers are run in a seperate thread
