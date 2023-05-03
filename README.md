# FriendTorrent
 

### Tasks:
	* [Done] Serialization/Desirialization BEencode library 
	* [Done] Parse peers list from tracker and connect to peers
	* [In progress] Work on message chunks for peers (Sending and Receiving packets)
	* [In progress] For multithreading instead of spinning a thread for each peer. Use async Tasks to download chunks, we can use logic where we download as many pieces available from a peer and then move on to others that more available chunks for us. 
	* [In progress] Code cleanup.
	

### Todo:
	 * UDP Tracking
	 * Implement piece download
	 * Implement peer seeding 
	 * Handle all Message types in Downloading Peer
	 * Handle Multifile lists in TFData for file size
	 * Resume from where we left off torrenting instead of doing a fresh download
	 * Add debug function call ie. #define DEBUGF(x) for logging
	 * Fix how we add available peers to TFData (currently done in SendRecvToTracker)


### Logic/Ideas
	* Create up to N threads for connections to peers. 
		** 1 Thread will be used to upload peices to connected peers
		** 1 Thread will be used to update all the trackers for the torrents we own
		** Remaining Threads will download missing torrent peices
	* Task/Logic:
		** Each thread will start by selecting a piece to download.
		** It will go through the list of unconnected peers until it finds one who has the piece it is looking for
		** It will then download that piece from the selected peer
		** When selecting the next piece to download, the thread will prioritize peices that its current peer has available


#### Useful Links:
	* http://www.bittorrent.org/beps/bep_0015.html
	* https://www.bittorrent.org/beps/bep_0003.html (Actual documentation)
	* https://wiki.theory.org/BitTorrentSpecification
	* https://www.seanjoflynn.com/research/bittorrent.html#trackers
	* http://www.kristenwidman.com/blog/33/how-to-write-a-bittorrent-client-part-1/




### Pseudo Documentation
Order of piece and block downloads:
	- Pieces downloads are random, each task is randomly assigned a piece to download from their connected peer (among the list of peices the peer owns)
		- This is to avoid collision between different tasks trying to download the same piece, and to prevent slowdowns from downloading the torrent sequentially
	- Blocks are downloaded sequentially for each piece (Potentially will change)
		- Tasks will download each peice starting from the first block, and will request the next block only after recieving the block it request
		- Todo: UDP (Resends)