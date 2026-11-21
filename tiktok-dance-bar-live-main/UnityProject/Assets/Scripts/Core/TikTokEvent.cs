using System;

namespace TikTokLiveGame
{
    [Serializable]
    public class TikTokEvent
    {
        public string type;
        public string eventId;
        public string state;
        public string message;
        public string userId;
        public string uniqueId;
        public string nickname;
        public string avatar;
        public string comment;
        public string giftId;
        public string giftName;
        public string action;
        public string masterRuleId;
        public string label;
        public string variant;
        public string titleLabel;
        public string titleVariant;
        public int repeatCount;
        public int comboCount;
        public int diamondCount;
        public int likeCount;
        public long roomHearts;
        public long personalHearts;
        public int heartGoal;
        public int score;
        public int giftPower;
        public int zoomDurationMs;
        public int durationMs;
        public int fireworkBursts;
        public bool spectatorOnly;
        public bool preview;
        public string testCommandId;
        public bool joinedNow;
        public long titleExpiresAt;
        public TikTokPlayerData[] players;
        public TikTokPlayerData[] vipScores;
        public GiftGuideItem[] guideItems;
        public string[] interactionHints;
        public HeartMilestone[] heartRules;
    }

    [Serializable]
    public class HeartMilestone
    {
        public string id;
        public string action;
        public int heartGoal;
        public int fireworkBursts;
        public int zoomDurationMs;
        public int durationMs;
    }

    [Serializable]
    public class GiftGuideItem
    {
        public string giftName;
        public string giftPictureUrl;
        public string label;
        public int diamondCount;
    }

    [Serializable]
    public class TikTokPlayerData
    {
        public bool hasGiftWing;
        public bool hasGiftAvatarFrame;
        public long personalHearts;
        public string userId;
        public string uniqueId;
        public string nickname;
        public string avatar;
        public string titleLabel;
        public string titleVariant;
        public int score;
        public int giftPower;
        public long titleExpiresAt;
    }

    [Serializable]
    internal class ClientMessage
    {
        public string type;
        public string role;
        public string testCommandId;
        public bool testActorMissing;
        public string username;
        public string action;
        public string giftName;
        public int count;
        public int userIndex;
        public int value;
    }
}
