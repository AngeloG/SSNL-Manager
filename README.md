SSNL Manager
============

Old server manager source code for SeriousSam.nl, can be used to host and admin dedicated servers for Serious Sam Revolution, Serious Sam HD (The First Encounter and The Second Encounter) and Serious Sam 3: BFE.

This source code is provided as is and there will not be any support for it. I will accept helpful pull requests if any. (Although Serious Sam 4 support might be added in the future :cat:)

---

## Running the manager

**Note**: This software is provided as-is, which is mostly a modified dump of the original source code and a (minified/templated) dump of the database. Not much support will be given and you're expected to understand how the dedicated servers work, as well as being familiar with PHP, MySQL, and C#.

To set up the whole ordeal, you'll need to set up a database first. To do this, run the `Template.sql` file on your database. From there you can add servers and administrator Steam ID's to the tables. (There is some sample data to get you started.) It's important that you change the default working directories located in the `games` table.

Then, in `Program.cs`, in the `Main` method, change the database hostname, username, and password. (The `Database` constructor. I know, this would be much better in a config file.) Build the project and run it, and it *should* work. Don't quote me on that.

## Configuring the web interface

First off, SeriousSam.nl was created to be integrated with the MyBB forum software (version 1.6). `server_acp.php` may need some modifications in order to work with newer versions of MyBB (and hell, probably PHP too). All of the code is inside of `server_acp.php`, which honestly isn't very pretty, but it was meant to be portable (somewhat) and easily installable aside of a MyBB installation. The entire script is also made to use url references to root ("/images/..." instead of "images/..."), so you will have to run it inside of a root domain (or subdomain), or modify the entire script (and associated css files).

Install MyBB, create your own administrator user on your new forum, and create a user group for "Server Admins". Users who are forum administrators or part of the "Server Admins" group will be so-called "global admins", who override the script's permissions table if it's set as their primary display group. The ID's for these groups are hardcoded on 2 occasions in `server_acp.php`, so you should make sure you change them if they're different for you. (Default: 4 = administrators, 8 = server admins)

You should edit the permissions table inside of `server_acp.php`, look for `$permissions`. This will grant you the ability to give different MyBB user groups power over different servers. This is not particularly required, but you should still modify it anyway, since you might accidentally give people administrator rights with the default settings.

When you're all done configuring the files, put `server_acp.php` in the root of your MyBB site. (Yep, right next to `index.php`.) There's probably some links you'll want to change to login pages and whatnot inside of that file, so make sure you skim through it at least once.
