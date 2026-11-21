function normalizeUser(data = {}) {
    const user = data.user || data;
    const profilePicture = user.profilePicture || {};
    const avatarSources = [
        profilePicture.urls,
        profilePicture.urlList,
        user.avatarThumb?.urlList,
        user.avatarMedium?.urlList,
        user.avatarLarge?.urlList,
        user.profilePictureUrls,
        user.profilePictureUrl,
    ]
        .flat()
        .filter((url) => typeof url === "string" && /^https:\/\//i.test(url));
    const avatar =
        avatarSources.find((url) => /\.(?:jpe?g|png)(?:[?~]|$)/i.test(url)) ||
        avatarSources[0] ||
        "";
    const username = user.uniqueId || user.displayId;
    const fallbackId = username || user.nickname || `guest-${Date.now()}`;

    return {
        userId: String(user.userId || user.id || fallbackId),
        uniqueId: String(username || fallbackId),
        nickname: String(user.nickname || username || "TikTok user"),
        avatar: String(avatar),
    };
}

function eventId(data = {}) {
    return String(data.event?.msgId || data.common?.msgId || data.msgId || "");
}

function normalizeChat(data) {
    return {
        type: "chat",
        eventId: eventId(data),
        ...normalizeUser(data),
        comment: String(data.comment || data.content || ""),
    };
}

function normalizeMember(data) {
    return {
        type: "member",
        eventId: eventId(data),
        ...normalizeUser(data),
    };
}

function normalizeGift(data) {
    const details = data.gift || data.giftDetails || {};
    const catalog = data.extendedGiftInfo || {};
    const repeatCount = Math.max(1, Number(data.repeatCount) || 1);
    const diamondCount = Math.max(
        0,
        Number(details.diamondCount) ||
            Number(catalog.diamondCount ?? catalog.diamond_count) ||
            Number(data.diamondCount) ||
            0,
    );

    return {
        type: "gift",
        eventId: eventId(data),
        ...normalizeUser(data),
        giftId: String(data.giftId || details.id || catalog.id || ""),
        giftName: String(
            details.name ||
                details.giftName ||
                catalog.name ||
                catalog.giftName ||
                data.giftName ||
                "Gift",
        ),
        giftType:
            Number(
                details.type ||
                    details.giftType ||
                    catalog.type ||
                    catalog.giftType ||
                    data.giftType,
            ) || 0,
        giftPictureUrl:
            details.image?.urlList?.[0] ||
            details.image?.url_list?.[0] ||
            details.giftImage?.url?.[0] ||
            catalog.image?.url_list?.[0] ||
            catalog.image?.urlList?.[0] ||
            data.giftPictureUrl ||
            "",
        repeatCount,
        repeatEnd: Number(data.repeatEnd) > 0,
        unitDiamondCount: diamondCount,
        diamondCount: diamondCount * repeatCount,
    };
}

function normalizeLike(data) {
    return {
        type: "like",
        eventId: eventId(data),
        ...normalizeUser(data),
        likeCount: Math.max(1, Number(data.likeCount ?? data.count) || 1),
        totalLikeCount: Math.max(
            0,
            Number(data.totalLikeCount ?? data.total) || 0,
        ),
    };
}

function normalizeSocial(type, data) {
    return {
        type,
        eventId: eventId(data),
        ...normalizeUser(data),
    };
}

function isPendingGiftStreak(event) {
    return event.giftType === 1 && !event.repeatEnd;
}

module.exports = {
    normalizeUser,
    normalizeChat,
    normalizeMember,
    normalizeGift,
    normalizeLike,
    normalizeSocial,
    isPendingGiftStreak,
};
