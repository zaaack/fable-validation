function isPlainObject(obj){
    if ( !obj || obj.toString() !== "[object Object]" || obj.nodeType || obj.setInterval ) {
        return false;
    }
     
    if ( obj.constructor && !obj.hasOwnProperty("constructor") && !obj.constructor.prototype.hasOwnProperty("isPrototypeOf") ) {
        return false;
    }
     
    var key;
    for ( key in obj ) {}
 
    return key === undefined || obj.hasOwnProperty(key);
}

export default function clone(a) {
    if (a instanceof Array) {
        return [...a]
    } else if (typeof a === 'object') {
        let Cls = a.constructor
        return new Cls()
    }
}