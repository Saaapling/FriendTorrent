# Bittorrent-2.0-TBD-
 

Tasks:
	- [Done] Serialization/Desirialization BEencode library 
	- [Done] Parse peers list from tracker and connect to peers
	- [In progress] Work on message chunks for peers (Sending and Receiving packets)
	- [In progress] For multithreading instead of spinning a thread for each peer. Use async Tasks to download chunks, we can use logic where we download as many pieces available from a peer and then move on to others that more available chunks for us. 
	

Todo:
	 - UDP Tracking
	 - Peerlist / Peer Class
	 - Implement peice download
	 - Open port for seeding 
	 - Handle other Message types in Downloading Peer


 Thread Logic/Ideas
	- Create up to N threads for connections to peers. 
		- 1 Thread will be used to upload peices to connected peers
		- Remaining Threads will download missing torrent peices
	- Thread Logic:
		- Each thread will start by selecting a peice to download.
		- It will go through the list of unconnected peers until it finds one who has the peice it is looking for
		- It will then download that peice from the selected peer
		- When selecting the next peice to download, the thread will prioritize peices that its current peer has available


Tracker Behavior:
	- 

Useful Links:
	- http://www.bittorrent.org/beps/bep_0015.html
	- https://www.bittorrent.org/beps/bep_0003.html
	- https://www.seanjoflynn.com/research/bittorrent.html#trackers
	- http://www.kristenwidman.com/blog/33/how-to-write-a-bittorrent-client-part-1/