import appUrl = require("common/appUrl");
import dialog = require("plugins/dialog");
import database = require("models/resources/database");
import EVENTS = require("common/constants/events");
import createResourceBase = require("viewmodels/resources/createResourceBase");
import dialogViewModelBase = require("viewmodels/dialogViewModelBase");
import getPluginsInfoCommand = require("commands/database/debug/getPluginsInfoCommand");
import getDatabaseStatsCommand = require("commands/resources/getDatabaseStatsCommand");
import getStatusDebugConfigCommand = require("commands/database/debug/getStatusDebugConfigCommand");
import getClusterTopologyCommand = require("commands/database/cluster/getClusterTopologyCommand");
import topology = require("models/database/replication/topology");
import shell = require("viewmodels/shell");
import resourcesManager = require("common/shell/resourcesManager");
import createDatabaseCommand = require("commands/resources/createDatabaseCommand");

import databaseCreationModel = require("models/resources/creation/databaseCreationModel");
import eventsCollector = require("common/eventsCollector");

class createDatabase extends createResourceBase {

    readonly databaseBundles: Array<availableBundle> = [
        {
            displayName: "Compression",
            name: "Compression",
            hasAdvancedConfiguration: false
        },
        {
            displayName: "Encryption",
            name: "Encryption",
            hasAdvancedConfiguration: true
        }
    ];

    resourceModel = new databaseCreationModel();

    indexesPathPlaceholder: KnockoutComputed<string>;

    getResourceByName(name: string): database {
        return resourcesManager.default.getDatabaseByName(name);
    }

    activate() {
        super.activate();

        //TODO: if cluster mode preselect replication bundle
        //TODO: if !!this.licenseStatus() && this.licenseStatus().IsCommercial && this.licenseStatus().Attributes.periodicBackup !== "true" preselect periodic export
        //TODO: fetchClusterWideConfig
        //TODO: fetchCustomBundles
    }

    protected initObservables() {
        super.initObservables();

        this.indexesPathPlaceholder = ko.pureComputed(() => {
            const name = this.resourceModel.name();
            return `~/${name || "{Database Name}"}/Indexes/`;
        });

        this.databaseBundles.forEach(bundle => {
            if (!bundle.hasOwnProperty('validationGroup')) {
                bundle.validationGroup = undefined;
            }
        });
    }

    getAvailableBundles() {
        //TODO: concat with custom bundles 
        return this.databaseBundles;
    }

    createResource() {
        eventsCollector.default.reportEvent('resource', 'create');

        const globalValid = this.isValid(this.resourceModel.globalValidationGroup);
        const advancedValid = this.isValid(this.resourceModel.advancedValidationGroup);

        const allValid = globalValid && advancedValid;

        if (allValid) {
            this.createResourceInternal();
        } else {
            if (!advancedValid && !this.advancedConfigurationVisible()) {
                this.showAdvancedConfiguration();
            }
        }
    }

    private createResourceInternal() {
        const databaseDocument = this.resourceModel.toDto();

        resourcesManager.default.activateAfterCreation(database.qualifier, databaseDocument.Id);

        new createDatabaseCommand(databaseDocument)
            .execute()
            .always(() => {
                dialog.close(this);
            });
    }

    private isBundleActiveComputed(bundleName: string) {
        return ko.pureComputed(() => _.includes(this.resourceModel.activeBundles(), bundleName));
    }

}

export = createDatabase;