﻿/// <reference path="../../../../../typings/tsd.d.ts"/>

import sqlColumn = require("models/database/tasks/sql/sqlColumn");
import sqlReference = require("models/database/tasks/sql/sqlReference");

abstract class abstractSqlTable {
    tableSchema: string;
    tableName: string;

    primaryKeyColumns = ko.observableArray<sqlColumn>([]);
    documentColumns = ko.observableArray<sqlColumn>([]);
    references = ko.observableArray<sqlReference>([]);

    getPrimaryKeyColumnNames() {
        return this.primaryKeyColumns().map(x => x.sqlName);
    }
    
    getLinkedReferencesDto() {
         return this.references()
            .filter(x => x.action() === 'link')
            .map(x => x.toLinkDto());
    }
    
    getEmbeddedReferencesDto() {
        return this.references()
            .filter(x => x.action() === 'embed')
            .map(x => x.toEmbeddedDto());
    }
    
    checkForDuplicateProperties() {
        const localProperties = this.documentColumns().map(x => x.propertyName());
        return localProperties.length !== _.uniq(localProperties).length;
    }
    
    getColumnsMapping() {
        const mapping = {} as dictionary<string>;
        this.documentColumns().forEach(column => {
            mapping[column.sqlName] = column.propertyName();
        });
        return mapping;
    }
    
    findLinksToTable(tableToFind: abstractSqlTable): Array<sqlReference> {
        const foundItems = this.references()
            .filter(x => x.action() === 'link' && x.targetTable.tableSchema === tableToFind.tableSchema && x.targetTable.tableName === tableToFind.tableName);
        
        this.references()
            .filter(x => x.action() === "embed")
            .map(ref => {
                foundItems.push(...ref.effectiveInnerTable().findLinksToTable(tableToFind));
            });
        
        return foundItems;
    }
    
    
    isManyToMany() {
        if (this.references().length !== 2) {
            // many-to-many should have 2 references
            return false;
        }
        
        if (_.some(this.references(), x => x.type !== "ManyToOne")) {
            // each reference should be manyToOne
            return false;
        }
        
        // at this point we have 2 many-to-one references
        const allJoinColumns = _.flatMap(this.references(), r => r.joinColumns);
        const primaryColumns = this.getPrimaryKeyColumnNames();
        
        return _.isEqual(allJoinColumns.sort(), primaryColumns.sort()); // references covers all primary key columns
    }
    
     setAllLinksToSkip() {
        this.references().forEach(reference => {
            if (reference.action() === 'link') {
                reference.skip();
            }
            
            if (reference.action() === 'embed') {
                reference.effectiveInnerTable().setAllLinksToSkip();
            }
        });
    }
    
}

export = abstractSqlTable;
