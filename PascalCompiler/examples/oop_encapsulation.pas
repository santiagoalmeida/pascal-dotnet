program OopEncapsulation;

type
  TAccount = class
  private
    balance: integer;
  public
    procedure Deposit(amount: integer);
    function GetBalance: integer;
  end;

procedure TAccount.Deposit(amount: integer);
begin
  balance := balance + amount; // acceso implícito, permitido: estamos dentro de la clase
end;

function TAccount.GetBalance: integer;
begin
  Result := balance;
end;

var
  acc: TAccount;
begin
  acc := TAccount.Create();
  acc.Deposit(100);
  acc.Deposit(50);
  writeln('Balance: ', acc.GetBalance());
end.
