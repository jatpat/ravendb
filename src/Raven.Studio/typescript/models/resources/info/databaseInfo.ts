/// <reference path="../../../../typings/tsd.d.ts"/>

import resourceInfo = require("models/resources/info/resourceInfo");
import database = require("models/resources/database");
import resourcesManager = require("common/shell/resourcesManager");

class databaseInfo extends resourceInfo {

    rejectClients = ko.observable<boolean>();
    indexingStatus = ko.observable<Raven.Client.Documents.Indexes.IndexRunningStatus>();
    indexingDisabled = ko.observable<boolean>();
    indexingPaused = ko.observable<boolean>();
    documentsCount = ko.observable<number>();
    indexesCount = ko.observable<number>();

    constructor(dto: Raven.Client.Server.Operations.DatabaseInfo) {
        super(dto);

        this.update(dto);
    }

    get qualifier() {
        return "db";
    }

    get fullTypeName() {
        return "database";
    }

    get urlPrefix() {
        return "databases";
    }

    asResource(): database {
        return resourcesManager.default.getDatabaseByName(this.name);
    }

    update(databaseInfo: Raven.Client.Server.Operations.DatabaseInfo): void {
        super.update(databaseInfo);
        this.rejectClients(databaseInfo.RejectClients);
        this.indexingStatus(databaseInfo.IndexingStatus);
        this.indexingDisabled(databaseInfo.IndexingStatus === "Disabled");
        this.indexingPaused(databaseInfo.IndexingStatus === "Paused");
        this.documentsCount(databaseInfo.DocumentsCount);
        this.indexesCount(databaseInfo.IndexesCount);
    }
}

export = databaseInfo;