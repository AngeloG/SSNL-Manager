/* SSNL Database Template */

SET FOREIGN_KEY_CHECKS=0;

-- ----------------------------
-- Table structure for `activeplayers`
-- ----------------------------
DROP TABLE IF EXISTS `activeplayers`;
CREATE TABLE `activeplayers` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `Server` int(11) NOT NULL,
  `Spectating` int(11) NOT NULL DEFAULT '1',
  `SteamID` varchar(16) NOT NULL,
  `Name` varchar(255) NOT NULL,
  `Frags` int(11) NOT NULL,
  `Deaths` int(11) NOT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records of activeplayers
-- ----------------------------

-- ----------------------------
-- Table structure for `adminactions`
-- ----------------------------
DROP TABLE IF EXISTS `adminactions`;
CREATE TABLE `adminactions` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `Date` datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  `Server` int(11) NOT NULL,
  `FromUserID` int(11) NOT NULL,
  `Reason` mediumtext NOT NULL,
  `Type` varchar(255) NOT NULL,
  `PlayerName` varchar(255) NOT NULL,
  `SteamID` varchar(255) NOT NULL,
  `Handled` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB AUTO_INCREMENT=44 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Table structure for `admins`
-- ----------------------------
DROP TABLE IF EXISTS `admins`;
CREATE TABLE `admins` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `SteamID` bigint(17) NOT NULL,
  `Rights` int(11) NOT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB AUTO_INCREMENT=3 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records of admins
-- ----------------------------
INSERT INTO `admins` VALUES ('1', '76561197991298608', '3');

-- ----------------------------
-- Table structure for `bans`
-- ----------------------------
DROP TABLE IF EXISTS `bans`;
CREATE TABLE `bans` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `FromUserID` int(11) NOT NULL,
  `Reason` mediumtext NOT NULL,
  `PlayerName` varchar(255) NOT NULL,
  `SteamID` varchar(255) NOT NULL,
  `BanTime` int(11) NOT NULL,
  `Time` int(11) NOT NULL COMMENT '0 = perma, 1 = 1 hour, 2 = 1 day',
  `Server` int(11) NOT NULL DEFAULT '-1',
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB AUTO_INCREMENT=6 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records of bans
-- ----------------------------
INSERT INTO `bans` VALUES ('5', '5', 'Annoying', 'JoeVandal', '110000107347f98', '1373973597', '86400', '2');

-- ----------------------------
-- Table structure for `games`
-- ----------------------------
DROP TABLE IF EXISTS `games`;
CREATE TABLE `games` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) NOT NULL,
  `Executable` varchar(255) NOT NULL,
  `WorkingDirectory` varchar(255) NOT NULL,
  `Color` int(11) NOT NULL,
  `HasStats` int(11) NOT NULL DEFAULT '1',
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB AUTO_INCREMENT=5 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records of games
-- ----------------------------
INSERT INTO `games` VALUES ('1', 'Serious Sam HD: The Second Encounter', 'SamHD_TSE_DedicatedServer.exe', 'g:\\Steam\\SteamApps\\common\\Serious Sam HD The Second Encounter\\Bin\\', '1', '1');
INSERT INTO `games` VALUES ('2', 'Serious Sam: Revolution', 'DedicatedServer.exe', 'g:\\Steam\\SteamApps\\common\\Serious Sam Revolution\\Bin\\', '2', '1');
INSERT INTO `games` VALUES ('3', 'Serious Sam 3: BFE', 'Sam3_DedicatedServer.exe', 'g:\\Steam\\SteamApps\\common\\Serious Sam 3\\Bin\\', '3', '1');
INSERT INTO `games` VALUES ('4', 'Serious Sam HD: The First Encounter', 'SamHD_DedicatedServer.exe', 'g:\\Steam\\SteamApps\\common\\Serious Sam HD The First Encounter\\Bin\\', '4', '0');

-- ----------------------------
-- Table structure for `log`
-- ----------------------------
DROP TABLE IF EXISTS `log`;
CREATE TABLE `log` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `Server` int(11) NOT NULL,
  `Log` mediumtext NOT NULL,
  PRIMARY KEY (`ID`),
  KEY `Server` (`Server`) USING HASH
) ENGINE=MyISAM DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records of log
-- ----------------------------

-- ----------------------------
-- Table structure for `servers`
-- ----------------------------
DROP TABLE IF EXISTS `servers`;
CREATE TABLE `servers` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `Game` int(11) NOT NULL DEFAULT '1',
  `Name` varchar(255) NOT NULL,
  `Gamemode` varchar(255) NOT NULL DEFAULT 'Deathmatch',
  `Maxplayers` int(11) NOT NULL DEFAULT '16',
  `Level` varchar(255) NOT NULL,
  `LevelList` varchar(255) NOT NULL DEFAULT 'Content/SeriousSamHD/Levels/Z5_Other/BrkeenChevap.wld',
  `Strict` int(11) NOT NULL DEFAULT '0',
  `Duel` int(11) NOT NULL DEFAULT '0',
  `Pups` int(11) NOT NULL DEFAULT '1',
  `InfiniteAmmo` int(11) NOT NULL DEFAULT '0',
  `DisallowedVotes` varchar(255) NOT NULL,
  `Port` int(11) NOT NULL DEFAULT '27015',
  `Active` int(11) NOT NULL DEFAULT '0',
  `Private` int(11) NOT NULL DEFAULT '0',
  `Extra` varchar(255) NOT NULL,
  `_PID` int(11) NOT NULL DEFAULT '0',
  `_Rcon` varchar(255) NOT NULL,
  `_Players` int(11) NOT NULL DEFAULT '0',
  `_LogLocation` int(11) NOT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB AUTO_INCREMENT=7 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records of servers
-- ----------------------------
INSERT INTO `servers` VALUES ('1', '1', '#01 - SeriousSam.nl', 'Deathmatch', '16', 'Content/SeriousSamHD/Levels/Z5_Other/BrkeenChevap.wld', 'SSNL/Maps_TSE_Versus.txt', '0', '0', '1', '0', '', '27015', '1', '0', '', '0', '', '0', '0');
INSERT INTO `servers` VALUES ('2', '1', '#02 - SeriousSam.nl', 'Deathmatch', '16', '', 'SSNL/Maps_TSE_Versus.txt', '0', '0', '1', '0', '', '27017', '1', '0', '', '0', '', '0', '0');
INSERT INTO `servers` VALUES ('3', '1', '#03 - SeriousSam.nl (needs whitelist)', 'InstantKill', '16', '', 'SSNL/Maps_TSE_Versus.txt', '0', '0', '1', '0', '', '27019', '1', '0', '', '0', '', '0', '0');

-- ----------------------------
-- Table structure for `whitelist`
-- ----------------------------
DROP TABLE IF EXISTS `whitelist`;
CREATE TABLE `whitelist` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `Server` int(11) NOT NULL,
  `SteamID` bigint(17) NOT NULL,
  `Note` varchar(255) NOT NULL,
  PRIMARY KEY (`ID`)
) ENGINE=InnoDB AUTO_INCREMENT=9 DEFAULT CHARSET=utf8;

-- ----------------------------
-- Records of whitelist
-- ----------------------------
INSERT INTO `whitelist` VALUES ('1', '3', '76561197991298608', 'Scratch');
INSERT INTO `whitelist` VALUES ('6', '3', '76561198061910206', 'Danker');
INSERT INTO `whitelist` VALUES ('7', '3', '76561198043141458', 'Yuka');
