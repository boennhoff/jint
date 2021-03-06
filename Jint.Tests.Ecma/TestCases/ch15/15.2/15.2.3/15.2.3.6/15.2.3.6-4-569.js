/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-569.js
 * @description ES5 Attributes - [[Get]] attribute is a function which contains global variable
 */


function testcase() {
        var obj = {};
        var globalVariable = 20;
        var getFunc = function () {
            globalVariable = 2010;
            return globalVariable;
        };

        Object.defineProperty(obj, "prop", {
            get: getFunc
        });
        var desc = Object.getOwnPropertyDescriptor(obj, "prop");

        return obj.hasOwnProperty("prop") && desc.get === getFunc && obj.prop === 2010 && globalVariable === 2010;
    }
runTestCase(testcase);
