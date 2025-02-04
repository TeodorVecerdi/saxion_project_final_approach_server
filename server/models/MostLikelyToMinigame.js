const questionAmount = 529;
module.exports = class MostLikelyTo {
    constructor(gameGuid, roomGuid, ownerGuid) {
        this.gameGuid = gameGuid;
        this.roomGuid = roomGuid;
        this.ownerGuid = ownerGuid;
        this.questions = Array();
        for (let i = 0; i <= questionAmount; i++) {
            this.questions.push(i);
        }
    }

    getQuestion() {
        if (this.questions.length > 0)
            return this.questions.splice(Math.floor(Math.random() * this.questions.length), 1)[0];
        return 0;
    }

    toJSON() {
        return {
            'gameGuid': this.gameGuid,
            'roomGuid': this.roomGuid,
            'ownerGuid': this.ownerGuid
        };
    }
};