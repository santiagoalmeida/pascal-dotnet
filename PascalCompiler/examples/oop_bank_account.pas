program OopBankAccount;

type
  TAccount = class
    owner: string;
    balance: integer;
    procedure Deposit(amount: integer);
    function Withdraw(amount: integer): boolean;
    procedure PrintStatus;
  end;

procedure TAccount.Deposit(amount: integer);
begin
  balance := balance + amount;
end;

function TAccount.Withdraw(amount: integer): boolean;
begin
  if amount > balance then
    Result := false
  else
  begin
    balance := balance - amount;
    Result := true;
  end;
end;

procedure TAccount.PrintStatus;
begin
  writeln(owner, ': $', balance);
end;

// Una funcion libre puede recibir objetos como parametros (se pasan por
// referencia, como cualquier tipo de clase): las mutaciones adentro de
// Transfer se ven reflejadas en las cuentas reales del llamador.
procedure Transfer(origen: TAccount; destino: TAccount; monto: integer);
begin
  if origen.Withdraw(monto) then
  begin
    destino.Deposit(monto);
    writeln('Transferencia de $', monto, ' de ', origen.owner, ' a ', destino.owner, ' OK');
  end
  else
    writeln('Fondos insuficientes en ', origen.owner);
end;

var
  cuentaA, cuentaB: TAccount;
begin
  cuentaA := TAccount.Create();
  cuentaA.owner := 'Ana';
  cuentaA.balance := 100;

  cuentaB := TAccount.Create();
  cuentaB.owner := 'Beto';
  cuentaB.balance := 20;

  writeln('--- Estado inicial ---');
  cuentaA.PrintStatus();
  cuentaB.PrintStatus();

  Transfer(cuentaA, cuentaB, 50);
  Transfer(cuentaA, cuentaB, 1000);

  writeln('--- Estado final ---');
  cuentaA.PrintStatus();
  cuentaB.PrintStatus();
end.
