using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Runtime.Serialization;
using SolidityAST;

namespace SolToBoogie
{
    public class ERC20SpecGenerator
    {
        private TranslatorContext context;
        private AST solidityAST;
        private List<String> ERC20Vars = new List<string>() {"totalSupply", "balanceOf", "allowance"};
        private List<String> ERC20fns = new List<string>() {"totalSupply", "balanceOf", "allowance", "approve", "transfer",
                             "transferFrom","increaseAllowance","decreaseAllowance", "burn", "burnFrom","mint"};
        private ContractDefinition entryContract;
        private Dictionary<String, VariableDeclaration> varDecls;
        private Dictionary<String, ContractDefinition> fnContracts;
        private Dictionary<VariableDeclaration, int> declToContractInd;
        private List<VariableDeclaration> otherVars;
        
        public ERC20SpecGenerator(TranslatorContext context, AST solidityAST, String entryPoint)
        {
            this.context = context;
            this.solidityAST = solidityAST;
            varDecls = new Dictionary<string, VariableDeclaration>();
            fnContracts = new Dictionary<string, ContractDefinition>();
            otherVars = new List<VariableDeclaration>();
            declToContractInd = new Dictionary<VariableDeclaration, int>();
            
            foreach (ContractDefinition def in context.ContractDefinitions)
            {
                if (def.Name.Equals(entryPoint))
                {
                    entryContract = def;
                }
            }

            int contractInd = 0;
            foreach (int id in entryContract.LinearizedBaseContracts)
            {
                contractInd++;
                ContractDefinition contract = context.GetASTNodeById(id) as ContractDefinition;

                if (context.ContractToStateVarsMap.ContainsKey(contract))
                {
                    otherVars.AddRange(context.ContractToStateVarsMap[contract]);
                    foreach (VariableDeclaration decl in context.ContractToStateVarsMap[contract])
                    {
                        declToContractInd[decl] = contractInd;
                    }
                }

                if (!context.ContractToFunctionsMap.ContainsKey(contract))
                {
                    continue;
                }

                HashSet<FunctionDefinition> fnDefs = context.ContractToFunctionsMap[contract];

                foreach (FunctionDefinition fnDef in fnDefs)
                {
                    if (ERC20fns.Contains(fnDef.Name) && !fnContracts.ContainsKey(fnDef.Name))
                    {
                        fnContracts[fnDef.Name] = contract;
                    }

                    if (ERC20Vars.Contains(fnDef.Name) && !varDecls.ContainsKey(fnDef.Name))
                    {
                        ReturnDeclarationFinder declFinder = new ReturnDeclarationFinder(context);
                        VariableDeclaration decl = declFinder.findDecl(contract, fnDef);
                        if (decl != null)
                        {
                            varDecls[fnDef.Name] = decl;
                        }
                    }
                }
            }

            foreach (VariableDeclaration decl in varDecls.Values)
            {
                otherVars.Remove(decl);
            }

            otherVars.RemoveAll(v => v.Constant);
        }

        private int CompareVars(VariableDeclaration v1, VariableDeclaration v2)
        {
            if (declToContractInd[v1] != declToContractInd[v2])
            {
                return declToContractInd[v1] > declToContractInd[v2] ? -1 : 1;
            }
            
            string[] v1Tokens = v1.Src.Split(':');
            int v1Pos = int.Parse(v1Tokens[0]);
            string[] v2Tokens = v2.Src.Split(':');
            int v2Pos = int.Parse(v2Tokens[0]);
            
            if (v1Pos != v2Pos)
            {
                return v1Pos < v2Pos ? -1 : 1;
            }

            throw new Exception("Two variables at the same position");
            /*int v1No = context.ASTNodeToSourceLineNumberMap[v1];
            int v2No = context.ASTNodeToSourceLineNumberMap[v2];

            if (v1No != v2No)
            {
                return v1No < v2No ? -1 : 1;
            }

            throw new Exception("Two variables declared on the same line");*/
        }

        public void GenerateSpec()
        {
            List<VariableDeclaration> allVars = new List<VariableDeclaration>(otherVars);
            String filename = context.ASTNodeToSourcePathMap[entryContract];
            StreamWriter writer = new StreamWriter($"{filename.Substring(0, filename.Length - 4)}.config");
            
            string totSupply = varDecls.ContainsKey("totalSupply") ? $"{varDecls["totalSupply"].Name}" : "";
            if (String.IsNullOrEmpty(totSupply))
            {
                Console.WriteLine("Warning: Could not find totalSupply variable");
            }
            else
            {
                allVars.Add(varDecls["totalSupply"]);
            }
            string bal = varDecls.ContainsKey("balanceOf") ? $"{varDecls["balanceOf"].Name}" : "";
            if (String.IsNullOrEmpty(bal))
            {
                Console.WriteLine("Warning: Could not find balance variable");
            }
            else
            {
                allVars.Add(varDecls["balanceOf"]);
            }
            string allowances = varDecls.ContainsKey("allowance") ? $"{varDecls["allowance"].Name}" : "";
            if (String.IsNullOrEmpty(allowances))
            {
                Console.WriteLine("Warning: Could not find allowances variable");
            }
            else
            {
                allVars.Add(varDecls["allowance"]);
            }
            
            allVars.Sort(CompareVars);
            
            string totContract = fnContracts.ContainsKey("totalSupply") ? fnContracts["totalSupply"].Name : "";
            string balContract = fnContracts.ContainsKey("balanceOf") ? fnContracts["balanceOf"].Name : "";
            string allowanceContract = fnContracts.ContainsKey("allowance") ? fnContracts["allowance"].Name : "";
            string approveContract = fnContracts.ContainsKey("approve") ? fnContracts["approve"].Name : "";
            string transferContract = fnContracts.ContainsKey("transfer") ? fnContracts["transfer"].Name : "";
            string transferFromContract =
                fnContracts.ContainsKey("transferFrom") ? fnContracts["transferFrom"].Name : "";
            string increaseAllowanceContract = 
                fnContracts.ContainsKey("increaseAllowanceContract") ? fnContracts["increaseAllowanceContract"].Name : "";
            string decreaseAllowanceContract = 
                fnContracts.ContainsKey("decreaseAllowanceContract") ? fnContracts["decreaseAllowanceContract"].Name : "";
			string burnContract = fnContracts.ContainsKey("burn") ? fnContracts["burn"].Name : "";
			string burnFromContract = fnContracts.ContainsKey("burnFrom") ? fnContracts["burnFrom"].Name : "";
			string mintContract = fnContracts.ContainsKey("mint") ? fnContracts["mint"].Name : "";

            string extraVars = String.Join(" ", otherVars.Select(v => $"this.{v.Name}"));

            writer.WriteLine($"FILE_NAME={filename}");
            writer.WriteLine($"CONTRACT_NAME={entryContract.Name}");
            writer.WriteLine($"TOTAL={totSupply}");
            writer.WriteLine($"BALANCES={bal}");
            writer.WriteLine($"ALLOWANCES={allowances}");
            writer.WriteLine($"TOT_SUP_CONTRACT={totContract}");
            writer.WriteLine($"BAL_OF_CONTRACT={balContract}");
            writer.WriteLine($"ALLOWANCE_CONTRACT={allowanceContract}");
            writer.WriteLine($"APPROVE_CONTRACT={approveContract}");
            writer.WriteLine($"TRANSFER_CONTRACT={transferContract}");
            writer.WriteLine($"TRANSFER_FROM_CONTRACT={transferFromContract}");
            writer.WriteLine($"INCREASE_ALLOWANCE_CONTRACT={increaseAllowanceContract}");
            writer.WriteLine($"DECREASE_ALLOWANCE_CONTRACT={decreaseAllowanceContract}");
            writer.WriteLine($"BURN_CONTRACT={burnContract}");
            writer.WriteLine($"BURN_FROM_CONTRACT={burnFromContract}");
            writer.WriteLine($"MINT_CONTRACT={mintContract}");
            writer.WriteLine($"EXTRA_VARS=({extraVars})");
            for (int i = 0; i < allVars.Count; i++)
            {
                writer.WriteLine($"{allVars[i].Name}={i}");
            }
            writer.Close();
            writeERC20Spec();
        }
        
        public void writeERC20Spec()
		{
            String filename = context.ASTNodeToSourcePathMap[entryContract];
            StreamWriter writer = new StreamWriter($"{filename.Substring(0, filename.Length - 4)}.spec");
 
    		string totSupply = varDecls.ContainsKey("totalSupply") ? $"{varDecls["totalSupply"].Name}" : "";
            string balances = varDecls.ContainsKey("balanceOf") ? $"{varDecls["balanceOf"].Name}" : "";
            string allowances = varDecls.ContainsKey("allowance") ? $"{varDecls["allowance"].Name}" : "";
            
            string totContract = fnContracts.ContainsKey("totalSupply") ? fnContracts["totalSupply"].Name : "";
            string balContract = fnContracts.ContainsKey("balanceOf") ? fnContracts["balanceOf"].Name : "";
            // string allowanceContract = fnContracts.ContainsKey("allowance") ? fnContracts["allowance"].Name : "";
            string approveContract = fnContracts.ContainsKey("approve") ? fnContracts["approve"].Name : "";
            string transferContract = fnContracts.ContainsKey("transfer") ? fnContracts["transfer"].Name : "";
            string transferFromContract = 
                fnContracts.ContainsKey("transferFrom") ? fnContracts["transferFrom"].Name : "";
            string increaseAllowanceContract = 
                fnContracts.ContainsKey("increaseAllowanceContract") ? fnContracts["increaseAllowanceContract"].Name : "";
            string decreaseAllowanceContract = 
                fnContracts.ContainsKey("decreaseAllowanceContract") ? fnContracts["decreaseAllowanceContract"].Name : "";
            string burnContract = fnContracts.ContainsKey("burn") ? fnContracts["burn"].Name : "";
			string burnFromContract = fnContracts.ContainsKey("burnFrom") ? fnContracts["burnFrom"].Name : "";
			string mintContract = fnContracts.ContainsKey("mint") ? fnContracts["mint"].Name : "";
            

			if (!String.IsNullOrEmpty(totSupply) && !String.IsNullOrEmpty(balances) && !String.IsNullOrEmpty(allowances))
			{
                string specs = "";
                string borne_sup = "0x10000000000000000000000000000000000000000000000000000000000000000";


                if(String.IsNullOrEmpty(mintContract) && String.IsNullOrEmpty(burnContract)){
                    // Total supply should change only by means of mint or burn
                    // totalSupply (TODO:CHECK IF ITS REALLY test_ERC20_constantSupply)
                    // ERC20-BASE-001
                    specs += "// ERC20-BASE-001\n";
                    specs += $"// #LTLProperty: [](finished(*, this.{totSupply} == old(this.{totSupply})))\n";
                }
				
				// User balance must not exceed total supply
				// test_ERC20_userBalanceNotHigherThanSupply
                // ERC20-BASE-002
                specs += "// ERC20-BASE-002\n";
				specs += $"// #LTLProperty: [](finished(*, this.{balances}[msg.sender] <= this.{totSupply}))\n";
	            
          		// Sum of users balance must not exceed total supply	
	            // test_ERC20_usersBalancesNotHigherThanSupply
                specs += "// ERC20-BASE-003\n";
				specs += $"// #LTLProperty: [](finished(*,sum(this.{balances}) <= this.{totSupply}))\n";
				
				// Address zero should have zero balance
				// test_ERC20_zeroAddressBalance
                specs += "// ERC20-BASE-004\n";
                specs += $"// #LTLProperty: [](finished(*,this.{balances}[null] == 0))\n";
				
                // Transfers to zero address should not be allowed
				// test_ERC20_transferToZeroAddress
                specs += "// ERC20-BASE-005\n";
				specs += $"// #LTLProperty: [](started({transferContract}.transfer(to,value), "+
				"to == null && "+
				$"value == this.{balances}[this] && "+
				$"this.{balances}[this] > 0) ==> "+
				$"<>(finished({transferContract}.transfer(to,value), "+
				"return == false && "+
				$"this.{totSupply} == old(this.{totSupply}) && "+
				$"this.{balances} == old(this.{balances}) && "+
				$"this.{allowances} == old(this.{allowances}))))\n";
				
				// Transfers to zero address should not be allowed
				// test_ERC20_transferFromToZeroAddress
                specs += "// ERC20-BASE-006\n";
				specs += $"// #LTLProperty: [](started({transferFromContract}.transferFrom(from, to, value), "+
				"from == msg.sender && "+
				"to == null && "+
                $"(value <= this.{balances}[msg.sender] || value <= this.{allowances}[msg.sender][this]) && " +
                $"value < {borne_sup} && " +
				$"this.{balances}[msg.sender] >= 0 &&  "+
				$"this.{balances}[msg.sender] < {borne_sup} && "+
				$"this.{allowances}[msg.sender][this] >= 0 && "+
				$"this.{allowances}[msg.sender][this] < {borne_sup}) ==> "+
				$"<>(finished({transferFromContract}.transferFrom(from, to, value), "+
				"return == false && "+
				$"this.{totSupply} == old(this.{totSupply}) && "+
				$"this.{balances} == old(this.{balances}) && "+
				$"this.{allowances}[msg.sender][this] == old(this.{allowances}[msg.sender][this]))))\n";
				
				
				// Self transfers should not break accounting
				// test_ERC20_selfTransferFrom
				// TODO: pas coherente avec erc20.spec
                specs += "// ERC20-BASE-007\n";
				specs += $"// #LTLProperty: [](started({transferFromContract}.transferFrom(from, to, value), "+
                "from == msg.sender && " +
                "from == to && " +
                $"(value <= this.{balances}[msg.sender] || value <= this.{allowances}[msg.sender][this]) && " +
                $"value < {borne_sup} && " +		
				$"this.{balances}[msg.sender] >= 0 &&  "+
				$"this.{balances}[msg.sender] < {borne_sup} && "+
				$"this.{allowances}[msg.sender][this] >= 0 && "+
				$"this.{allowances}[msg.sender][this] < {borne_sup}) ==> "+
			    $"<>(finished({transferFromContract}.transferFrom(from, to, value), "+
				"return == true && "+
				$"this.{balances}[msg.sender] == old(this.{balances}[msg.sender]))))\n";
				
			 	// Self transfers should not break accounting
				// test_ERC20_selfTransfer
                specs += "// ERC20-BASE-008\n";
				specs += $"// #LTLProperty: [](started({transferContract}.transfer(to, value), "+
                "to == this && "+
                $"value <= this.{balances}[this] && "+
				"value > 0 && "+
				$"value < {borne_sup} && "+
				$"this.{balances}[this] > 0 &&  "+
				$"this.{balances}[this] < {borne_sup}) ==> "+
			    $"<>(finished({transferContract}.transfer(to, value), "+
				"return == true && "+
				$"this.{balances}[this] == old(this.{balances}[this]))))\n";
				
				
				// Transfers for more than available balance should not be allowed
				// test_ERC20_transferFromMoreThanBalance
                specs += "// ERC20-BASE-009\n";
				specs += $"// #LTLVariables: p1:Ref\n";
				specs += $"// #LTLProperty: [](started({transferFromContract}.transferFrom(from, to, value),"+
				"from == msg.sender && "+
                "p1 != from && p1 != to && "+
				$"value > this.{balances}[msg.sender] && "+
                $"value < this.{borne_sup} && "+
				$"this.{balances}[to] >= 0 && "+
                $"this.{balances}[to] < {borne_sup} && "+
				$"this.{balances}[msg.sender] > 0 && "+
				$"this.{balances}[msg.sender] < {borne_sup} && "+
				$"this.{allowances}[msg.sender][this] > this.{balances}[from]) ==> "+
				$"<>(finished({transferFromContract}.transferFrom(from, to, value),"+
				"return == false && "+
				$"this.{balances}[msg.sender] == old(this.{balances}[msg.sender]) && "+
				$"this.{balances}[to] == old(this.{balances}[to] && "+ 
                $"this.{totSupply} == old(this.{totSupply}) && "+
				$"this.{balances}[p1] == old(this.{balances}[p1]) && "+
				$"this.{allowances} == old(this.{allowances}))))\n";
				

				// Transfers for more than available balance should not be allowed
				// test_ERC20_transferMoreThanBalance
                specs += "//  ERC20-BASE-010\n";
				specs += $"// #LTLVariables: p1:Ref\n";
				specs += $"// #LTLProperty: [](started({transferContract}.transfer(to,value),  "+
				"p1 != this && p1 != to && "+
                $"value > this.{balances}[this] && "+
                $"value < this.{borne_sup} && "+
				$"this.{balances}[to] >= 0 && "+
                $"this.{balances}[to] < {borne_sup} && "+
				$"this.{balances}[this] > 0 && "+
                $"this.{balances}[this] < {borne_sup}) ==> "+
				$"<>(finished({transferContract}.transfer(to,value), "+
				"return == false && "+
				$"this.{balances}[this] == old(this.{balances}[this]) && "+
				$"this.{balances}[to] == old(this.{balances}[to]) && "+
                $"this.{totSupply} == old(this.{totSupply}) && "+
				$"this.{balances}[p1] == old(this.{balances}[p1]) && "+
				$"this.{allowances} == old(this.{allowances}))))\n";

				// Zero amount transfers should not break accounting
				// test_ERC20_transferZeroAmount
                specs += "// ERC20-BASE-011\n";
				specs += $"// #LTLProperty: [](started({transferContract}.transfer(to,value), "+
				"to != this && "+
				"value == 0 && "+
				$"this.{balances}[this] > 0) ==> "+
				$"<>(finished({transferContract}.transfer(to,value),"+
				"return == true && "+
				$"this.{balances}[this] == old(this.{balances}[this]) && "+
				$"this.{balances}[to] == old(this.{balances}[to]))))\n";
				
				
				// Zero amount transfers should not break accounting
				// test_ERC20_transferFromZeroAmount
                specs += "// ERC20-BASE-012\n";
				specs += $"// #LTLProperty: [](started({transferFromContract}.transferFrom(from, to, value),"+
				"from == msg.sender && "+
				"to != from && "+
				"value == 0  && "+
				$"this.{balances}[msg.sender] > 0 && "+
				$"this.{allowances}[msg.sender][this] > 0 ) ==> "+
				$"<>(finished({transferFromContract}.transferFrom(from, to, value),"+
				"return == true && "+
				$"this.{balances}[test_ERC20_transferMoreThanBalance] == old(this.{balances}[test_ERC20_transferMoreThanBalance]) && "+
				$"this.{balances}[to] == old(this.{balances}[to]))))\n";
				
				
				// Transfers should update accounting correctly
				// test_ERC20_transfer
                specs += "// ERC20-BASE-013\n";
                specs += $"// #LTLVariables: p1:Ref\n";
				specs += $"// #LTLProperty: [](started({transferContract}.transfer(to,value), "+
				"p1 != this &&  p1 != to && "+
				"to != this && "+
				$"this.{balances}[to] + value < {borne_sup} && "+ 
				"value > 0  && "+
				$"value <= this.{balances}[this] && "+ 
				$"this.{balances}[this] > 2) ==> "+
			    $"<>(finished({transferContract}.transfer(to, value), "+
				"return == true && "+
				$"this.{balances}[this] == old(this.{balances}[this]) - value &&  "+
				$"this.{balances}[to] == old(this.{balances}[to]) + value && "+
				$"this.{totSupply} == old(this.{totSupply}) &&  "+
				$"this.{allowances} == old(this.{allowances}) &&  "+
				$"this.{balances}[p1] == old(this.{balances}[p1]))))\n";
				
				 // Transfers should update accounting correctly
				// test_ERC20_transferFrom
                specs += "// ERC20-BASE-014\n";
				specs += $"// #LTLVariables: p1:Ref\n";
				specs += $"// #LTLProperty: [](started({transferFromContract}.transferFrom(from, to, value),"+
				"p1 != this && p1 != to && "+
				"to != this && "+
				"to != msg.sender && "+
				$"this.{balances}[msg.sender] > 2 && "+
				$"this.{allowances}[msg.sender][this] > this.{balances}[msg.sender] && "+
				$"this.{balances}[to] + value < {borne_sup} && "+ 
				"value > 0  && "+
				$"value <= this.{balances}[this] ) ==> "+
				$"<>(finished({transferFromContract}.transferFrom(from, to, value),"+
				"return == true && "+
				$"this.{balances}[msg.sender] == old(this.{balances}[msg.sender]) - value &&  "+
				$"this.{balances}[to] == old(this.{balances}[to]) + value && "+
				$"this.{totSupply} == old(this.{totSupply}) &&  "+
				$"this.{allowances} == old(this.{allowances}) &&  "+
				$"this.{balances}[p1] == old(this.{balances}[p1]))))\n";
				
				
				
				// Approve should set correct allowances
				// test_ERC20_setAllowance
                specs += "// ERC20-BASE-015\n";
				specs += $"// #LTLProperty: [](finished({approveContract}.approve(to, value),"+
				"return == true && "+
				$"this.{allowances}[this][to] == value))\n";
				
				
				
				// Approve should set correct allowances
				// test_ERC20_setAllowanceTwice
                specs += "// ERC20-BASE-016\n";
				specs += $"// #LTLProperty: [](finished({approveContract}.approve(to, value),"+
				"return == true && "+
				$"this.{allowances}[this][to] == value) && "+
				$"(X(finished({approveContract}.approve(to, value), "+
				"return == true && "+
				$"this.{allowances}[this][to] == value * 0.5)))))\n";
				
				// TransferFrom should decrease allowance
				// test_ERC20_spendAllowanceAfterTransfer
                specs += "// ERC20-BASE-017\n";
				specs += $"// #LTLProperty: [](started({transferFromContract}.transferFrom(from, to, value),"+
				"from == msg.sender &&  "+
				"to != this && "+ 
				"to != msg.sender && "+
				"to != null && "+
				"value > 0 && "+
				$"value <= this.{balances}[msg.sender] && "+
				$"this.{balances}[msg.sender] > 0 && "+
				$"this.{allowances}[msg.sender][this] > this.{balances}[msg.sender]) ==> "+
				$"<>(finished({transferFromContract}.transferFrom(from, to, value),"+
				"return == true && "+
				$"this.{allowances}[msg.sender][this] == old(this.{allowances}[msg.sender][this]) - value)))\n";
				
				
				
                if (!String.IsNullOrEmpty(burnContract)){
                    // Burn should update user balance and total supply
                    // test_ERC20_burn
                    specs += "// ERC20-BURNABLE-001\n";
                    specs += $"// #LTLProperty: [](started({burnContract}.burn(amount),"+
                    $"this.{balances}[this] > 0 && "+
                    "amount >= 0 && "+
                    $"amount <= this.{balances}[this]) ==> "+
                    $"<>(finished({burnContract}.burn(amount), "+
                    $"this.{balances}[this] == old(this.{balances}[this]) - amount && "+
                    $"this.{totSupply} == old(this.{totSupply}) - amount)))\n";
                }
				
                if (!String.IsNullOrEmpty(burnFromContract)){
                    // Burn should update user balance and total supply
                    // test_ERC20_burnFrom
                    specs += "// ERC20-BURNABLE-002\n";
                    specs += $"// #LTLProperty: [](started({burnFromContract}.burnFrom(from, amount),"+
                    "from == msg.sender && "+ 
                    "amount >= 0 && "+
                    $"amount < this.{balances}[msg.sender] && "+
                    $"this.{balances}[msg.sender] > 0 && "+
                    $"this.{allowances}[msg.sender][this] > this.{balances}[msg.sender]) ==> "+
                    $"<>(finished({burnFromContract}.burnFrom(from, amount),"+
                    $"this.{balances}[msg.sender] == old(this.{balances}[msg.sender]) - amount && "+
                    $"this.{totSupply} == old(this.{totSupply}) - amount)))\n";
                
				
                    // Burn should update user balance and total supply
                    // test_ERC20_burnFromUpdateAllowance
                    specs += "// ERC20-BURNABLE-003\n";
                    specs += $"// #LTLProperty: [](started({burnFromContract}.burnFrom(from, amount),"+
                    "from == msg.sender && "+
                    $"this.{balances}[msg.sender] > 0 && "+ 
                    $"this.{allowances}[msg.sender][this] > this.{balances}[msg.sender] && "+
                    "amount >= 0 && "+
                    $"amount <= this.{balances}[msg.sender] ) ==> "+
                    $"<>(finished({burnFromContract}.burnFrom(from, amount),"+
                    $"old(this.{allowances}[from][this]) < {borne_sup} && "+
                    $"this.{allowances}[from][this] == old(this.{allowances}[from][this]) - amount)))\n";
				}


                if (!String.IsNullOrEmpty(mintContract)){
                    // Minting tokens should update user balance and total supply
                    // test_ERC20_mintTokens
                    specs += "// ERC20-MINTABLE-001\n";
                    specs += $"// #LTLProperty: [](finished({mintContract}.mint(to, amount),"+
                    $"this.{balances}[to] == old(this.{balances}[to]) + amount && "+
                    $"this.{totSupply} == old(this.{totSupply}) + amount))\n";
                }
				
				// TODO: Tests for pausable tokens
				// TODO: Tests for tokens implementing increaseAllowance and decreaseAllowance
				

				
                if (!String.IsNullOrEmpty(approveContract) && !String.IsNullOrEmpty(increaseAllowanceContract)){   
                    // Allowance should be modified correctly via increase/decrease
                    // test_ERC20_setAndIncreaseAllowance
                    specs += "// ERC20-ALLOWANCE-001\n";
                    specs += $"// #LTLVariables: initialAmount:int\n";
                    specs += $"// #LTLProperty: [](started({approveContract}.approve(to, value)"+
                    "return == true && "+
                    "value == initialAmount && "+
                    $"this.{allowances}[this][to] == value) ==>"+
                    $"<>(finished({increaseAllowanceContract}.increaseAllowance(to,value),"+
                    "return == true && "+
                    $"this.{allowances}[this][to] == initialAmount + value)))\n";
                }
				
                if (!String.IsNullOrEmpty(approveContract) && !String.IsNullOrEmpty(decreaseAllowanceContract)){  

                    // Allowance should be modified correctly via increase/decrease
                    // test_ERC20_setAndDecreaseAllowance
                    specs += "// ERC20-ALLOWANCE-002\n";
                    specs += $"// #LTLVariables: initialAmount:int\n";
                    specs += $"// #LTLProperty: [](started({approveContract}.approve(to, value),"+
                    "return == true && "+
                    "value == initialAmount && "+ 
                    $"this.{allowances}[this][to] == value) ==>"+
                    $"<>(finished({decreaseAllowanceContract}.decreaseAllowance(to,value),"+
                    "return == true && "+
                    $"this.{allowances}[this][to] == initialAmount - value)))\n";
                }
                writer.WriteLine(specs);
                writer.Close();
			}
		}


        private class ReturnDeclarationFinder : BasicASTVisitor
        {
            private VariableDeclaration retDecl;
            private String findVar;
            private TranslatorContext context;
            private ContractDefinition curContract;
            
            public ReturnDeclarationFinder(TranslatorContext context)
            {
                this.context = context;
                retDecl = null;
            }

            public VariableDeclaration findDecl(ContractDefinition curContract, FunctionDefinition def)
            {
                if (def.Body == null)
                {
                    return null;
                }
                
                if (def.ReturnParameters.Parameters.Count != 1)
                {
                    return null;
                }

                if (!String.IsNullOrEmpty(def.ReturnParameters.Parameters[0].Name))
                {
                    findVar = def.ReturnParameters.Parameters[0].Name;
                }

                this.curContract = curContract;
                
                def.Body.Accept(this);
                return retDecl;
            }

            public override bool Visit(ArrayTypeName node)
            {
                return false;
            }

            public override bool Visit(Assignment node)
            {
                if (node.LeftHandSide is Identifier ident)
                {
                    if (ident.Name.Equals(findVar))
                    {
                        node.RightHandSide.Accept(this);
                    }
                }
                return false;
            }

            public override bool Visit(SourceUnit node)
            {
                return false;
            }

            public override bool Visit(BinaryOperation node)
            {
                return false;
            }

            public override bool Visit(Block node)
            {
                for (int i = node.Statements.Count - 1; i >= 0; i--)
                {
                    node.Statements[i].Accept(this);
                }
                return false;
            }

            public override bool Visit(Break node)
            {
                return false;
            }

            public override bool Visit(Conditional node)
            {
                return false;
            }

            public override bool Visit(Continue node)
            {
                return false;
            }

            public override bool Visit(ContractDefinition node)
            {
                return false;
            }

            public override bool Visit(PragmaDirective node)
            {
                return false;
            }

            public override bool Visit(DoWhileStatement node)
            {
                return false;
            }

            public override bool Visit(ElementaryTypeName node)
            {
                return false;
            }

            public override bool Visit(ElementaryTypeNameExpression node)
            {
                return false;
            }

            public override bool Visit(EmitStatement node)
            {
                return false;
            }

            public override bool Visit(EnumDefinition node)
            {
                return false;
            }

            public override bool Visit(UsingForDirective node)
            {
                return false;
            }

            public override bool Visit(ImportDirective node)
            {
                return false;
            }

            public override bool Visit(InheritanceSpecifier node)
            {
                return false;
            }

            public override bool Visit(EnumValue node)
            {
                return false;
            }

            public override bool Visit(EventDefinition node)
            {
                return false;
            }

            public override bool Visit(ExpressionStatement node)
            {
                return false;
            }

            public override bool Visit(ForStatement node)
            {
                return false;
            }

            public FunctionDefinition findFn(string fnName, bool usesSuper)
            {
                List<int> searchContracts = new List<int>(curContract.LinearizedBaseContracts);

                if (!usesSuper)
                {
                    searchContracts.Insert(0, curContract.Id);
                }
                
                foreach (int id in searchContracts)
                {
                    ContractDefinition def = context.IdToNodeMap[id] as ContractDefinition;
                    HashSet < FunctionDefinition > fnDefs = context.ContractToFunctionsMap[def];
                    Dictionary<String, FunctionDefinition> nameToFn = fnDefs.ToDictionary(v => v.Name, v => v);
                    if (nameToFn.ContainsKey(fnName))
                    {
                        return nameToFn[fnName];
                    }
                }

                return null;
            }

            public override bool Visit(FunctionCall node)
            {
                if (node.Expression is Identifier call)
                {
                    FunctionDefinition def = findFn(call.Name, false);
                    if (def != null && def.ReturnParameters.Parameters.Count != 0)
                    {
                        def.Body.Accept(this);
                    }
                }
                else if (node.Expression is MemberAccess access)
                {
                    if (access.Expression is Identifier ident && ident.Name.Equals("super"))
                    {
                        FunctionDefinition def = findFn(access.MemberName, true);
                        if (def != null && def.ReturnParameters.Parameters.Count != 0)
                        {
                            def.Body.Accept(this);
                        }
                    }
                }

                return false;
            }

            public override bool Visit(FunctionDefinition node)
            {
                return false;
            }

            public override bool Visit(Identifier node)
            {
                int id = node.ReferencedDeclaration;
                VariableDeclaration varDecl = context.IdToNodeMap[id] as VariableDeclaration;

                if (varDecl.StateVariable)
                {
                    retDecl = varDecl;
                    return false;
                }
                
                findVar = node.Name;
                return false;
            }

            public override bool Visit(ParameterList node)
            {
                return false;
            }

            public override bool Visit(ModifierDefinition node)
            {
                return false;
            }

            public override bool Visit(IfStatement node)
            {
                return false;
            }

            public override bool Visit(ModifierInvocation node)
            {
                return false;
            }

            public override bool Visit(StructDefinition node)
            {
                return false;
            }

            public override bool Visit(IndexAccess node)
            {
                node.BaseExpression.Accept(this);
                return false;
            }

            public override bool Visit(VariableDeclaration node)
            {
                return false;
            }

            public override bool Visit(UserDefinedTypeName node)
            {
                return false;
            }

            public override bool Visit(InlineAssembly node)
            {
                return false;
            }

            public override bool Visit(Literal node)
            {
                return false;
            }

            public override bool Visit(Mapping node)
            {
                return false;
            }

            public override bool Visit(MemberAccess node)
            {
                return false;
            }

            public override bool Visit(NewExpression node)
            {
                return false;
            }

            public override bool Visit(PlaceholderStatement node)
            {
                return false;
            }

            public override bool Visit(WhileStatement node)
            {
                return false;
            }

            public override bool Visit(Return node)
            {
                node.Expression.Accept(this);
                return false;
            }

            public override bool Visit(SourceUnitList node)
            {
                return false;
            }

            public override bool Visit(Throw node)
            {
                return false;
            }

            public override bool Visit(UnaryOperation node)
            {
                return false;
            }

            public override bool Visit(TupleExpression node)
            {
                return false;
            }

            public override bool Visit(VariableDeclarationStatement node)
            {
                return false;
            }
        }
    }
}
