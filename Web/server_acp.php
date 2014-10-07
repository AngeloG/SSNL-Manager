<?php
/*
	The MIT License (MIT)

	Copyright (c) 2014 Angelo Geels (angelog.nl, spansjh@gmail.com)

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/

//NOTE: Every comment prefixed with "NOTE:" has been written 2 years after the discontuniation of SeriousSam.nl servers.
//NOTE: SSNL admin web-panel requires a MyBB back-end for user authentication!
//      Please read through the first couple comments carefully to know what you can configure.
if(!isset($_COOKIE['mybbuser'])) {
	//NOTE: This link directs to a "steamlogin" page which is a MyBB plugin for Steam login.
	die("Not logged in, please log in <a href=\"/misc.php?action=steamlogin\">here</a>.");
}

error_reporting(E_ALL);
ini_set("display_errors", "1");

include("inc/config.php");

$db = new mysqli(
	$config['database']['hostname'],
	$config['database']['username'],
	$config['database']['password'],
	$config['database']['database']);
if($db->connect_error) {
	die($db->connect_error);
}

define("MYBB", $config['database']['table_prefix']);

function sqlSafe($str) {
	global $db;
	return $db->real_escape_string($str);
}

function htmlSafe($str) {
	return htmlentities($str, ENT_QUOTES);
}

$parse = explode("_", $_COOKIE['mybbuser']);
$userID = intval($parse[0]);
$userKey = sqlSafe($parse[1]);

$res = $db->query("SELECT * FROM ".MYBB."users WHERE uid=" . $userID . " AND loginkey='" . $userKey . "'");
$thisUser = false;
if($res->num_rows > 0) {
	$thisUser = $res->fetch_array();
}

if(!$thisUser) {
	die("Failed to fetch your user from the databsae.");
}

// permissions table (array of MyBB forum group ID's, with array of server ID's)
//NOTE: This really would've been better in database!
$permissions = array(
	// HD Versus
	"10" => array(1, 2, 3),
	// HD Coop
	"11" => array(1, 2, 3),
	// HD Insta
	"12" => array(1, 2),
	// BFE Coop
	"13" => array(1)
);

$thisPermissions = array();
$isGlobalAdmin = $thisUser['displaygroup'] == 4 || $thisUser['displaygroup'] == 8; // MyBB group ID's for "Administrators" and "Server Admins"

if($thisUser['additionalgroups'] != "") {
	$groups = explode(",", $thisUser['additionalgroups']);
	for($i=0; $i<count($groups); $i++) {
		$gid = $groups[$i];
		
		if(!isset($permissions[$gid])) {
			echo "No such permission set for gid " . $gid;
			continue;
		}
		
		$perms = $permissions[$gid];
		for($j=0; $j<count($perms); $j++) {
			$thisPermissions[] = $perms[$j];
		}
	}
}

if(count($thisPermissions) == 0 && !$isGlobalAdmin) {
	die("No permissions, please contact admin");
}

$db_internal = new mysqli("localhost", "ssnl", "", "ssnl", 3307);
if($db_internal->connect_error) {
	die("Internal db failure: (maybe the servers are offline? contact admin!!!!) " . $db_internal->connect_error);
}

function ProperGameMode($gameMode)
{
	$ret = "";
	for($i=0; $i<strlen($gameMode); $i++) {
		if(ctype_upper($gameMode[$i]) && $i != 0) {
			$ret .= " " . $gameMode[$i];
		} else {
			$ret .= $gameMode[$i];
		}
	}
	return $ret;
}

function CanUseServer($server_id)
{
	global $thisPermissions;
	global $isGlobalAdmin;
	
	return $isGlobalAdmin || in_array($server_id, $thisPermissions);
}

function Block($title, $content)
{
	?>
  <table class="tborder" cellspacing="0" cellpadding="5" border="0">
    <tr>
      <td class="thead"><strong><?php echo $title; ?></strong></td>
    </tr>
    <tr>
      <td class="trow1">
        <?php echo $content; ?>
      </td>
    </tr>
  </table>
  <?php
}

/* ActionBlock
 * Used to render a block with action info.
 *  $action = the action name, for example "enable_server"
 *  $title = the title to show, for example "Enable server A?"
 *  $question = the more detailed question to ask, for example "Are you sure you want to enable A?"
 *  $actionText = the text in the button, for example "Enable this server"
 *  $server = the server ID, for example intval($_POST['server'])
 *  $extra = extra content which will be in the <form> tag (typically for extra <input> fields)
 */
function ActionBlock($action, $title, $question, $actionText, $server, $extra = "")
{
	if($action == "") {
		return;
	}
	
	Block($title, "<p>" . $question . "</p>
		<form method=\"post\" action=\"/server_acp.php\">
			<input type=\"hidden\" name=\"session\" value=\"" . htmlSafe($_COOKIE['mybbuser']) . "\" />
			<input type=\"hidden\" name=\"action\" value=\"" . $action . "\" />
			<input type=\"hidden\" name=\"server\" value=\"" . $server . "\" />
			" . $extra . "
			<p>Reason for this action: <input type=\"text\" name=\"reason\" value=\"\" style=\"width: 500px;\" autocomplete=\"off\" /></p>
			<p><input class=\"button\" type=\"submit\" value=\"" . htmlSafe($actionText) . "\" />
			<a href=\"/server_acp.php\">No, go back</a></p>
		</form>");
}

function xTimeAgo($oldTime)
{
	$timeCalc = time() - $oldTime;
	$timeType = "x";
	if($timeCalc >= 0) {
		$timeType = "s";
	}
	if($timeCalc >= 60) {
		$timeType = "m";
	}
	if($timeCalc >= (60*60)) {
		$timeType = "h";
	}
	if($timeCalc >= (60*60*24)) {
		$timeType = "d";
	}
	if($timeType == "s") {
		$timeCalc .= " seconds ago";
	}
	if($timeType == "m") {
		$num = round($timeCalc/60);
		$timeCalc = $num . " minute" . ($num == 1 ? "" : "s") . " ago";
	}
	if($timeType == "h") {
		$num = round($timeCalc/60/60);
		$timeCalc = $num . " hour" . ($num == 1 ? "" : "s") . " ago";
	}
	if($timeType == "d") {
		$num = round($timeCalc/60/60/24);
		$timeCalc = $num . " day" . ($num == 1 ? "" : "s") . " ago";
	}
	return $timeCalc;
}

//NOTE: This function sends an adminaction to the server backend, then the server backend handles the requested action (should be within 1 second) and marks the row as handled.
function AdminAction($server, $type, $reason = "", $playername = "", $steamid = "", $handled = false)
{
	global $db_internal;
	global $thisUser;
	$db_internal->query("INSERT INTO adminactions (`Date`,`Server`,`FromUserID`,`Reason`,`Type`,`PlayerName`,`SteamID`,`Handled`)" .
		" VALUES(NOW()," . intval($server) . "," . $thisUser['uid'] . ",'" . sqlSafe($reason) . "','" . sqlSafe($type) . "','" . sqlSafe($playername) . "','" . sqlSafe($steamid) . "'," . ($handled ? "1" : "0") . ")");
}

if(isset($_POST['action'])) {
	if($_POST['session'] !== $_COOKIE['mybbuser']) {
		die("Hacking attempt blocked - please notify admin of this!");
	}
	
	$server = intval($_POST['server']);
	if(CanUseServer($server)) {
		$action = $_POST['action'];
		$reason = $_POST['reason'];
		
		switch($action) {
			case "cancel_vote":
				AdminAction($server, "cancel_vote", $reason, "", "");
				break;
			
			case "kick_player":
				$steamID = $_POST['steamid'];
				$name = $_POST['name'];
				
				AdminAction($server, "kick", $reason, $name, $steamID);
				break;
			
			case "ban_player":
				$steamID = $_POST['steamid'];
				$name = $_POST['name'];
				$time = $_POST['time'];
				
				$db_internal->query("INSERT INTO bans (`FromUserID`,`Reason`,`PlayerName`,`SteamID`,`BanTime`,`Time`,`Server`)" .
					" VALUES(" . $thisUser['uid'] . ",'" . sqlSafe($reason) . "','" . sqlSafe($name) . "','" . sqlSafe($steamID) . "'," . time() . "," . intval($_POST['time']) . "," . $server . ")");
				AdminAction($server, "ban", $reason, $name, $steamID);
				break;
			
			case "enable_server":
				$db_internal->query("UPDATE servers SET Active=1 WHERE ID=" . $server);
				AdminAction($server, "enable", $reason, "", "", true);
				break;
			
			case "disable_server":
				$db_internal->query("UPDATE servers SET Active=0 WHERE ID=" . $server);
				AdminAction($server, "disable", $reason, "", "", true);
				break;
			
			case "edit_server":
				// TODO: This could be done better :(
				$query = "UPDATE servers SET" .
						" `Game`=" . intval($_POST['serverr']['Game']) .
						",`Name`='" . sqlSafe($_POST['serverr']['Name']) . "'" .
						",`Gamemode`='" . sqlSafe($_POST['serverr']['Gamemode']) . "'" .
						",`Maxplayers`=" . intval($_POST['serverr']['Maxplayers']) .
						",`Level`='" . sqlSafe($_POST['serverr']['Level']) . "'" .
						",`LevelList`='" . sqlSafe($_POST['serverr']['LevelList']) . "'" .
						",`Strict`=" . (isset($_POST['serverr']['Strict']) ? "1" : "0") .
						",`Duel`=" . (isset($_POST['serverr']['Duel']) ? "1" : "0") .
						",`Pups`=" . (isset($_POST['serverr']['Pups']) ? "1" : "0") .
						",`InfiniteAmmo`=" . (isset($_POST['serverr']['InfiniteAmmo']) ? "1" : "0") .
						",`DisallowedVotes`='" . sqlSafe($_POST['serverr']['DisallowedVotes']) . "'" .
						",`Active`=" . (isset($_POST['serverr']['Active']) ? "1" : "0") .
						",`Private`=" . (isset($_POST['serverr']['Private']) ? "1" : "0") .
						",`Extra`='" . sqlSafe($_POST['serverr']['Extra']) . "'" .
					" WHERE ID=" . $server;
				
				$db_internal->query($query);
				AdminAction($server, "edit", $reason, "", "", true);
				break;
		}
	}
	
	header("Location: /server_acp.php");
	exit;
}

header("Content-type: text/html; charset=UTF-8");

$resAdmins = $db->query("SELECT * FROM ".MYBB."users WHERE additionalgroups!='' OR usergroup!=2");
$_admins = array();
$_adminsSuper = array();
while($rowAdmin = $resAdmins->fetch_array()) {
	// MyBB group ID for VIP users
	if($rowAdmin['additionalgroups'] == "9") {
		continue;
	}
	
	$_admins[] = $rowAdmin['loginname'];
	$_adminsSuper[$rowAdmin['loginname']] = $rowAdmin['additionalgroups'] == "8" || $rowAdmin['usergroup'] == "4"; // MyBB group ID's for "Server Admins" and "Administrators"
}

//NOTE: I don't think I ever finished the mobile interface stylesheet, so use with caution
$mobile = false;
if(strstr($_SERVER['HTTP_USER_AGENT'], "Android")) {
	$mobile = true;
}

if($mobile) {
	ob_start();
}

?>
<!DOCTYPE html>
<html>
	<head>
  	<title>SeriousSam.nl Server Admin CP<?php if($mobile) { echo " (Mobile)"; } ?></title>
    <meta http-equiv="Content-Type" content="text/html; charset=UTF-8" />
    <?php
		if($mobile) {
			?>
      <link type="text/css" rel="stylesheet" href="/server_acp_mobile.css" />
      <meta name="viewport" content="width=device-width, initial-scale=1.0, user-scalable=no" />
      <meta name="format-detection" content="telephone=no" />
      <meta name="format-detection" content="address=no" />
      <?php
		} else {
			?>
			<link type="text/css" rel="stylesheet" href="/server_acp/css3.css" />
			<link type="text/css" rel="stylesheet" href="/server_acp/global.css" />
			<style>
				#logo h1 {
					color: #FFF;
					margin: 20px;
				}
				.editme[type="text"] {
					width: 500px;
				}
			</style>
      <?php
		}
		?>
  </head>
  <body>
  	<div id="container">
    	<div id="header">
      	
      	<div id="logo">
        	<div class="wrapper">
            <a href="/server_acp.php">
            	<?php
							if($mobile) {
								?>SeriousSam.nl Server Admin CP<?php
							} else {
								?><img title="SeriousSam.nl Serious Sam servers" alt="SeriousSam.nl Serious Sam servers" src="/images/night/logo.png"><?php
							}
							?>
            </a>
          </div>
        </div>
        
        <div id="panel">
        	<div class="upper">
          	<div class="wrapper">
            	
              <?php if(!$mobile) { ?>
                <ul class="menu top_links">
                  <li><a href="/index.php">Forum index</a></li>
                </ul>
              <?php } ?>
              
              <span class="welcome">
              	<strong>Welcome, <?php echo $thisUser['username']; ?>.</strong>
                <?php
								if($mobile) {
									echo "<br />";
								}
								if($isGlobalAdmin) {
									if($mobile) { echo "<div class=\"mobilebutton\">"; }
									?>
                  <a href="?page=modlog"><?php if(!$mobile) { ?><img src="/images/server_acp/icons/information.png" /> <?php } ?>Moderation log</a>
                  <a href="?page=bans"><?php if(!$mobile) { ?><img src="/images/server_acp/icons/delete.png" /> <?php } ?>Bans</a>
                  <?php
									if($mobile) { echo "</div>"; }
								}
								?>
              </span>
              
            </div>
          </div>
        </div>
        
        <div id="content">
        	<div class="wrapper">
          	
						<?php
						if(isset($_GET['action'])) {
							$action = $_GET['action'];
							$title = ""; // block title
							$question = ""; // more descriptive block question
							$actionText = ""; // button text
							$server = 0; // server ID
							$extra = ""; // additional html in <form> tag
							
							switch($_GET['action']) {
								case "cancel_vote":
									$server = intval($_GET['server']);
									$resServer = $db_internal->query("SELECT * FROM servers WHERE ID=" . $server);
									$rowServer = $resServer->fetch_array();
									
									$title = "Cancel vote on " . $rowServer['Name'] . "?";
									$question = "Are you sure you want to cancel the currently ongoing vote on " . $rowServer['Name'] . "?";
									$actionText = "Cancel the vote";
									break;
								
								case "kick_player":
									$server = intval($_GET['server']);
									$steamID = htmlSafe($_GET['steamid']);
									$name = htmlSafe($_GET['name']);
									
									$title = "Kick " . $name . "?";
									$question = "Are you sure you want to kick <b>" . $name . "</b> with Steam ID <b>" . $steamID . "</b>?";
									$actionText = "Kick this player";
									$extra = "<input type=\"hidden\" name=\"steamid\" value=\"" . $steamID . "\" />" .
										"<input type=\"hidden\" name=\"name\" value=\"" . $name . "\" />";
									break;
								
								case "ban_player":
									$server = intval($_GET['server']);
									$steamID = htmlSafe($_GET['steamid']);
									$name = htmlSafe($_GET['name']);
									
									$title = "Ban " . $name . "?";
									$question = "Are you sure you want to ban <b>" . $name . "</b> with Steam ID <b>" . $steamID . "</b>?";
									$actionText = "Ban this player";
									$extra = "<input type=\"hidden\" name=\"steamid\" value=\"" . $steamID . "\" />" .
										"<input type=\"hidden\" name=\"name\" value=\"" . $name . "\" />" .
										"Server: <select name=\"Server\">" .
											"<option value=\"-1\">All servers</option>";
									$resGames = $db_internal->query("SELECT * FROM games");
									while($rowGame = $resGames->fetch_array()) {
										$resServers = $db_internal->query("SELECT * FROM servers WHERE `Game`=" . $rowGame['ID'] . " AND `Active`=1");
										if($resServers->num_rows > 0) {
											$extra .= "<optgroup label=\"" . $rowGame['Name'] . "\">";
											while($rowServer = $resServers->fetch_array()) {
												if($rowServer['ID'] == $server) {
													$extra .= "<option value=\"" . $rowServer['ID'] . "\" selected>" . $rowServer['Name'] . "</option>";
												} else {
													$extra .= "<option value=\"" . $rowServer['ID'] . "\">" . $rowServer['Name'] . "</option>";
												}
											}
											$extra .= "</optgroup>";
										}
									}
									$extra .= "</select>";
									//NOTE: Ugly.. should've been done with a PHP array.
									$extra .= "<p>Ban time:<br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"0\" /><b>Permanently</b></label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 5) . "\" />5 minutes</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 10) . "\" checked />10 minutes</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 30) . "\" />30 minutes</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60) . "\" />1 hour</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 2) . "\" />2 hours</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 4) . "\" />4 hours</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 8) . "\" />8 hours</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24) . "\" />1 day</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24 * 2) . "\" />2 days</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24 * 3) . "\" />3 days</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24 * 4) . "\" />4 days</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24 * 5) . "\" />5 days</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24 * 7) . "\" />1 week</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24 * 7 * 2) . "\" />2 weeks</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24 * 30) . "\" />1 month (30 days)</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24 * 30 * 2) . "\" />2 months</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24 * 30 * 3) . "\" />3 months</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24 * 30 * 4) . "\" />4 months</label><br />" .
										"<label><input type=\"radio\" name=\"time\" value=\"" . (60 * 60 * 24 * 365) . "\" />1 year</label><br />" .
										"</p>";
									break;
								
								case "enable_server":
									$server = intval($_GET['server']);
									$resServer = $db_internal->query("SELECT * FROM servers WHERE ID=" . $server);
									$rowServer = $resServer->fetch_array();
									
									$title = "Enable server " . $rowServer['Name'] . "?";
									$question = "Are you sure you want to enable " . $rowServer['Name'] . "?";
									$actionText = "Enable this server";
									break;
								
								case "disable_server":
									$server = intval($_GET['server']);
									$resServer = $db_internal->query("SELECT * FROM servers WHERE ID=" . $server);
									$rowServer = $resServer->fetch_array();
									
									$title = "Disable server " . $rowServer['Name'] . "?";
									$question = "Are you sure you want to disable " . $rowServer['Name'] . "?";
									$actionText = "Disable this server";
									break;
								
								case "edit_server":
									$server = intval($_GET['server']);
									$resServer = $db_internal->query("SELECT * FROM servers WHERE ID=" . $server);
									$rowServer = $resServer->fetch_array();
									
									$title = "Edit server " . $rowServer['Name'];
									$question = "You are editing " . $rowServer['Name'] . ".";
									$actionText = "Save server";
									$extra = "<table>";
									foreach($rowServer as $k => $v) {
										if(is_int($k) || $k[0] == '_') {
											continue;
										}
										$type = "text";
										$info = "";
										if($k == "Game") { $type = "select"; }
										if($k == "Name") { $info = "Use [play] for the controller icon"; }
										if($k == "Level") { $info = "Level filename to start when server starts - if empty, a random one from cycle file below is chosen"; }
										if($k == "LevelList") { $info = "List of levels to use for cycling - contact admin for more info"; }
										if($k == "Strict" || $k == "Pups" || $k == "Duel" || $k == "Active" || $k == "Private" || $k == "InfiniteAmmo") { $type = "checkbox"; }
										if($k == "Strict") { $info = "<a href=\"/strict/\" target=\"_blank\">Click here</a> for info about what this does"; }
										if($k == "Duel") { $info = "This is &quot;press F3 to ready&quot; mode"; }
										if($k == "Pups") { $info = "Checked to enable powerups, unchecked to disable them"; }
										if($k == "DisallowedVotes") { $info = "Ask admin for the correct formatting and available options, or leave empty or set to \"all\" or \"changegamemode\""; }
										if($k == "Active") { $info = "Checked to turn the server on, unchecked to turn it off"; }
										if($k == "Private") { $info = "Please ask admin to set this up, it involves a whitelist"; }
										if($k == "Extra") { $info = "You probably don't have to bother with this"; }
										$select = "";
										if($type == "select") {
											$select = "<select name=\"serverr[" . $k . "]\">";
											$resGames = $db_internal->query("SELECT * FROM games");
											while($rowGame = $resGames->fetch_array()) {
												$select .= "<option value=\"" . $rowGame['ID'] . "\">" . $rowGame['Name'] . "</option>";
											}
											$select .= "</select>";
										}
										$extra .= "<tr>
											<td>" . ($type == "checkbox" ? "" : $k) . "</td>
											<td>" . ($type == "select" ? $select : "<label>
													" . (($k == "ID" || $k == "Port") ? $v : "<input class=\"editme\" type=\"" . $type . "\" name=\"serverr[" . $k . "]\" " . ($type == "text" ? "value=\"" . htmlSafe($v) . "\"" : ($v == "1" ? "checked" : "")) . " />") . "
												" . ($type == "checkbox" ? $k : "") . "</label>") . "
											</td>
											<td><i>" . $info . "</i></td>
										</tr>";
									}
									$extra .= "</table>";
									// TODO: This needs a "reset" adminaction.
									break;
								
								default: $action = ""; break;
							}
							
							if(CanUseServer($server)) {
								ActionBlock($action, $title, $question, $actionText, $server, $extra);
							}
						} else if(isset($_GET['page'])) {
							switch($_GET['page']) {
								case "modlog":
									if(!$isGlobalAdmin) {
										break;
									}
									
									$content = "<table cellspacing=\"0\" cellpadding=\"5\" border=\"0\" width=\"100%\">
										<tr>
											<td class=\"tcat\" width=\"15%\"><strong>Time</strong></td>
											<td class=\"tcat\" width=\"15%\"><strong>By who</strong></td>
											<td class=\"tcat\" width=\"25%\"><strong>Server</strong></td>
											<td class=\"tcat\" width=\"45%\"><strong>Action</strong></td>
										</tr>";
									$resActions = $db_internal->query("SELECT adminactions.*, servers.Name AS ServerName FROM adminactions LEFT JOIN servers ON adminactions.Server=servers.ID ORDER BY adminactions.ID DESC");
									$count = 0;
									
									$userCache = array();
									
									while($action = $resActions->fetch_array()) {
										$tdStart = " class=\"trow" . ($count % 2 + 1) . "\" valign=\"top\"";
										$content .= "<tr>
											<td" . $tdStart . ">" . $action['Date'] . "<br />" . xTimeAgo(strtotime($action['Date'])) . "</td>";
										
										if(!isset($userCache[$action['FromUserID']])) {
											$resFromUser = $db->query("SELECT * FROM ".MYBB."users WHERE uid=" . $action['FromUserID']);
											$userCache[$action['FromUserID']] = $resFromUser->fetch_array();
										}
										$userFrom = $userCache[$action['FromUserID']];
										$content .= "<td" . $tdStart . "><a href=\"/member.php?action=profile&uid=" . $userFrom['uid'] . "\">" . htmlSafe($userFrom['username']) . "</a></td>";
										$content .= "<td" . $tdStart . ">" . $action['ServerName'] . "</td>";
										
										$actionText = "<pre>" . print_r($action, true) . "</pre>";
										
										switch($action['Type']) {
											case "cancel_vote":
												$actionText = "Canceled ongoing vote";
												break;
											
											case "kick":
												$actionText = "Kicked <b>" . htmlSafe($action['PlayerName']) . "</b> with Steam ID " .
													"<a href=\"http://steamcommunity.com/profiles/" . hexdec($action['SteamID']) . "\">" . $action['SteamID'] . "</a>";
												break;
											
											case "enable":
												$actionText = "Enabled <b>" . $action['ServerName'] . "</b>";
												break;
											
											case "disable":
												$actionText = "Disabled <b>" . $action['ServerName'] . "</b>";
												break;
											
											case "ban":
												$actionText = "Banned <b>" . htmlSafe($action['PlayerName']) . "</b> on <b>" . $action['ServerName'] . "</b>";
												break;
											
											case "edit":
												$actionText = "Edited <b>" . htmlSafe($action['ServerName']) . "</b>";
												break;
										}
										
										$actionText .= "<br />Reason: ";
										if($action['Reason'] == "") {
											$actionText .= "<strong style=\"color: #F00;\">No reason!</strong>";
										} else {
											$actionText .= "<i>" . htmlSafe($action['Reason']) . "</i>";
										}
										$content .= "<td" . $tdStart . ">" . $actionText . "</td>";
										
										$content .= "</tr>";
										$count++;
									}
									$content .= "</table>";
									
									Block("Moderation log", "<p>Below are the last 50 server admin actions.</p>\n" . $content);
									break;
								
								case "bans":
									if(!$isGlobalAdmin) {
										break;
									}
									
									$content = "<table cellspacing=\"0\" cellpadding=\"5\" border=\"0\" width=\"100%\">
										<tr>
											<td class=\"tcat\" width=\"15%\"><strong>From</strong></td>
											<td class=\"tcat\" width=\"15%\"><strong>To</strong></td>
											<td class=\"tcat\" width=\"12.5%\"><strong>Who</strong></td>
											<td class=\"tcat\" width=\"12.5%\"><strong>By who</strong></td>
											<td class=\"tcat\" width=\"22.5%\"><strong>Where</strong></td>
											<td class=\"tcat\" width=\"22.5%\"><strong>Reason</strong></td>
										</tr>";
									
									$userCache = array();
									
									$resBans = $db_internal->query("SELECT bans.*, servers.Name as ServerName FROM bans LEFT JOIN servers ON bans.Server=servers.ID ORDER BY servers.ID DESC, `Time` DESC");
									$count = 0;
									while($rowBan = $resBans->fetch_array()) {
										if(!isset($userCache[$rowBan['FromUserID']])) {
											$resFromUser = $db->query("SELECT * FROM ".MYBB."users WHERE uid=" . $rowBan['FromUserID']);
											$userCache[$rowBan['FromUserID']] = $resFromUser->fetch_array();
										}
										
										$expired = time() > ($rowBan['BanTime'] + $rowBan['Time']);
										
										$strTimeFormat = "Y-m-d H:i:s"; // 2013-07-14 17:57:46
										$from = date($strTimeFormat, $rowBan['BanTime']);
										if($rowBan['Time'] == 0 ) {
											$to = "<b>&infin;</b> permabanned";
											$expired = false;
										} else {
											$to = date($strTimeFormat, $rowBan['BanTime'] + $rowBan['Time']);
										}
										
										$tdStart = " class=\"trow" . ($count % 2 + 1) . "\" valign=\"top\"";
										if($expired) {
											$tdStart .= " style=\"color: #aaa !important;\"";
										}
										
										$content .= "<tr>";
										$content .= "<td" . $tdStart . ">" . $from . "</td>";
										$content .= "<td" . $tdStart . ">" . $to . "</td>";
										$content .= "<td" . $tdStart . "><a href=\"http://steamcommunity.com/profiles/" . hexdec($rowBan['SteamID']) . "\">" . htmlSafe($rowBan['PlayerName']) . "</a></td>";
										
										$userFrom = $userCache[$rowBan['FromUserID']];
										$content .= "<td" . $tdStart . "><a href=\"/member.php?action=profile&uid=" . $userFrom['uid'] . "\">" . htmlSafe($userFrom['username']) . "</a></td>";
										
										$content .= "<td" . $tdStart . ">" . ($rowBan['ServerName'] == "" ? "<b>All servers</b>" : $rowBan['ServerName']) . "</td>";
										$content .= "<td" . $tdStart . "><i>" . htmlSafe($rowBan['Reason']) . "</i></td>";
										$content .= "</tr>";
										
										$count++;
									}
									
									$content .= "</table>";
									
									Block("Bans", "<p>Below are all banned players:</p>\n" . $content);
									break;
							}
						} else {
							$res = $db_internal->query("SELECT * FROM games");
							while($game = $res->fetch_array()) {
								$resServers = $db_internal->query("SELECT * FROM servers WHERE Game=" . $game['ID'] . " ORDER BY Active DESC");
								
								$servers = array();
								while($server = $resServers->fetch_array()) {
									if(CanUseServer($server['ID'])) {
										$servers[] = $server;
									}
								}
								
								if(count($servers) == 0) {
									continue;
								}
								?>
								<table class="tborder" cellspacing="0" cellpadding="5" border="0" width="100%">
									<thead>
										<tr>
											<td class="thead" colspan="6">
												<img src="/images/server_acp/games/<?php echo $game['ID']; ?>.png"<?php if($mobile) { echo " width=\"20\""; } ?> />
												<strong><?php echo $game['Name']; ?></strong>
                        <?php
												$hasWebStats = $game['HasStats'] == "1";
												if(!$hasWebStats) {
													echo " - <strong style=\"color: #F00;\">Doesn't have web stats, providing Rcon passwords!</strong>";
												}
												?>
											</td>
										</tr>
									</thead>
									<tbody>
										<tr>
											<td class="tcat" width="2%"><span class="smalltext"><strong>ID</strong></span></td>
											<td class="tcat" width="38%"><span class="smalltext"><strong>Name</strong></span></td>
											<td class="tcat" width="5%" align="center"><span class="smalltext"><strong>Active</strong></span></td>
											<td class="tcat" width="10%" align="center"><span class="smalltext"><strong>Gamemode</strong></span></td>
											<td class="tcat" width="10%" align="center"><span class="smalltext"><strong>Players</strong></span></td>
											<td class="tcat" width="35%" align="center"><span class="smalltext"><strong>Actions</strong></span></td>
										</tr>
										<?php
										for($serverIndex=0; $serverIndex<count($servers); $serverIndex++) {
											$server = $servers[$serverIndex];
											
											$tdStart = " class=\"trow" . ($serverIndex % 2 + 1) . "\" valign=\"top\"";
											
											$gameMode = $server['Gamemode'];
											$properGameMode = ProperGameMode($gameMode);
											
											?>
											<tr>
												<td<?php echo $tdStart; ?>><?php echo $server['ID']; ?></td>
												<td<?php echo $tdStart; ?>>
                        	<img src="/images/server_acp/icons/server.png" /> <?php echo $server['Name'];
													if($isGlobalAdmin && isset($_GET['pids'])) { echo " (" . $server['_PID'] . ")"; }
													if(!$hasWebStats) { echo " (Rcon password: <i>" . $server['_Rcon'] . "</i> on port <i>" . $server['Port'] . "</i>)"; } ?></td>
												<td<?php echo $tdStart; ?> align="center"><img src="/images/server_acp/icons/<?php echo $server['Active'] == "1" ? "accept" : "delete"; ?>.png" /></td>
												<td<?php echo $tdStart; ?> align="center"><?php echo $properGameMode; ?></td>
												<td<?php echo $tdStart; ?> align="center"><?php echo $server['_Players']; ?> / <?php echo $server['Maxplayers']; ?></td>
												<td<?php echo $tdStart; ?> align="center">
                        	<a href="?action=log_server&server=<?php echo $server['ID']; ?>"><img src="/images/server_acp/icons/server_chart.png" /> Log</a>
													<?php if($server['Active'] == "1") { ?>
														<a href="?action=disable_server&server=<?php echo $server['ID']; ?>"><img src="/images/server_acp/icons/server_delete.png" /> Disable</a>
                            <a href="?action=cancel_vote&server=<?php echo $server['ID']; ?>"><img src="/images/server_acp/icons/delete.png" /> Cancel vote</a>
													<?php } else { ?>
														<a href="?action=edit_server&server=<?php echo $server['ID']; ?>"><img src="/images/server_acp/icons/server_edit.png" /> Edit</a>
														<a href="?action=enable_server&server=<?php echo $server['ID']; ?>"><img src="/images/server_acp/icons/server_add.png" /> Enable</a>
													<?php } ?>
												</td>
											</tr>
											<?php
											if($server['_Players'] > 0) {
												$resPlayers = $db_internal->query("SELECT * FROM activeplayers WHERE server=" . $server['ID'] . " ORDER BY `Spectating` ASC, `Frags` DESC");
												?>
												<tr>
													<td class="tcat"></td>
													<td class="tcat"><strong>Name</strong></td>
													<td class="tcat" align="center"><strong>Steam</strong></td>
													<td class="tcat" align="center"><strong>Kills</strong></td>
													<td class="tcat" align="center"><strong>Deaths</strong></td>
													<td class="tcat" align="center"><strong>Actions</strong></td>
												</tr>
												<?php
												while($player = $resPlayers->fetch_array()) {
													$steamID = hexdec($player['SteamID']);
													?>
													<tr>
														<td<?php echo $tdStart; ?>>&nbsp;</td>
														<td<?php echo $tdStart; ?>><img src="/images/server_acp/icons/user.png" /> <?php echo /*htmlSafe(*/($player['Name'] == "" ? "<i>Unknown yet</i>" : $player['Name']);
															if($player['Spectating'] == "1") { echo " <i>(Spectator)</i>"; }
															if(in_array($steamID, $_admins)) {
																if($_adminsSuper[$steamID]) {
																	echo " <strong style=\"color: #A00;\"><i>(Super Admin)</i></strong>";
																} else {
																	echo " <strong style=\"color: #0A0;\"><i>(Admin)</i></strong>";
																}
															}
														?></td>
														<td<?php echo $tdStart; ?> align="center"><a href="http://steamcommunity.com/profiles/<?php echo $steamID; ?>" target="_blank"><img src="/images/server_acp/icons/steam.png" /></a></td>
														<td<?php echo $tdStart; ?> align="center"><?php echo $player['Frags']; ?></td>
														<td<?php echo $tdStart; ?> align="center"><?php echo $player['Deaths']; ?></td>
														<td<?php echo $tdStart; ?> align="center">
                            	<a href="?action=log_player&server=<?php echo $server['ID']; ?>&steamid=<?php echo $player['SteamID']; ?>&name=<?php echo htmlSafe($player['Name']); ?>"><img src="/images/server_acp/icons/user_comment.png" /> Log</a>
															<a href="?action=kick_player&server=<?php echo $server['ID']; ?>&steamid=<?php echo $player['SteamID']; ?>&name=<?php echo htmlSafe($player['Name']); ?>"><img src="/images/server_acp/icons/user_delete.png" /> Kick</a>
                              <a href="?action=ban_player&server=<?php echo $server['ID']; ?>&steamid=<?php echo $player['SteamID']; ?>&name=<?php echo htmlSafe($player['Name']); ?>"><img src="/images/server_acp/icons/delete.png" /> Ban</a>
														</td>
													</tr>
													<?php
												}
												?>
												<?php
											}
										}
										?>
									</tbody>
								</table>
								<br />
								<?php
							}
						}
            ?>
            
          </div>
        </div>
        
      </div>
    </div>
    
    <?php
		if($mobile) {
			?>
      <script>
				/* hide url bar on mobile safari and android (except chrome for android... :c) */
				window.scrollTo(0, 1);
			</script>
      <?php
		}
		?>
		
		<div style="text-align: center;">Powered by <a href="https://github.com/AngeloG/SSNL-Manager">SeriousSam.nl Server Manager</a></div>
		<?php /* Please do not remove above "powered by" message! <3 ... although I won't blame you. */ ?>
  </body>
</html>
<?php

if($mobile) {
	$page = ob_get_contents();
	
	if(!isset($_GET['no_compression'])) {
		$removeMe = str_split("\r\t");
		$removeMe[] = "  ";
		$page = str_replace($removeMe, "", $page);
	}
	
	ob_end_clean();
	echo $page;
}

$db->close();
?>