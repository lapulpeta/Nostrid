# How to use the Follows editor

It is fairly common for Nostr users to lose their follows. The reason for this is that follows are set all at once.
That is, when you follow someone, you send all your follow list to the network. Normally this works fine when
you use a single client, but things may go awry if you use multiple clients. For example, suppose you follow
account A in client X, then go and follow account B in client Y _before_ Y knows that you followed A in client X.
In this case you lose follow A from client X as this change is overwritten by Y.

Nostrid has a Follows editor that can help you. This tool shows you the accounts that you currently follow and the
accounts that can be seen that you followed at some point in the past. This data is taken from relays and also
from the local database that Nostrid has in your device. Most relays discard old follows, but it may happen that
some relays still have your old follows. Also Nostrid never deletes your old follows (unless you manually clean the
database), so if some other client has overwritten your follows, they will for sure be visible in the Follows editor.

To open the Follows editor, do this:

1. Log in
2. In the main menu, go to `Me` (that's your profile)

![Nostrid Android](https://raw.githubusercontent.com/lapulpeta/Nostrid-media/main/follows-editor1.jpeg)

3. Select 'Edit follows'

![Nostrid Android](https://raw.githubusercontent.com/lapulpeta/Nostrid-media/main/follows-editor2.jpeg)

Now you should see the Follows editor. Here you can see your current and past follows (if available). Select the
accounts that you want to follow and unselect any that you don't want to follow. Once you are ready, just
press `Update`. This will send your new list to the network.

If there are still missing follows, try connecting to other relays and maybe they will have an old copy of your
follows. If they do then they will be visible in the Follows editor.

You can repeat this process as many times as you need, even from different devices. This is important because
sometimes you have Nostrid in your main device and also a copy in some secondary device, and this one may have
an old copy of your follows.