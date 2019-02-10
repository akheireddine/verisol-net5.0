pragma solidity ^0.4.24;


contract A {
   uint [2] l;
   bool [2] aa;
   string[3] n;
   uint [2][2] m;

    constructor () public {
        l[0] = 1;
        l[1] = 2;
        delete l[1];
        assert (l[1] == 0); //simple scalar case
 
        aa[0] = true;
        delete aa[0];
        assert (!aa[0]);

        n[0] = "a";
        n[1] = "b";
        string memory s = "";
        delete n[0];
        bytes32 b1 = keccak256(bytes(s));
        bytes32 b2 = keccak256(bytes(n[0])); 
        assert (b1 ==  b2);


/*        
        delete n;
        assert (keccak256(bytes(s)) == keccak256(bytes(n[1])));

        m[0][0] = 1;
        m[0][1] = 2;
        m[1][1] = 3;
        m[1][0] = 4;
        delete m[0];
        assert (m[0][0] == 0);
        assert (m[0][1] == 0);
        delete m;
        assert (m[1][1] == 0);
*/
    }
    
}