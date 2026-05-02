.PHONY: sim test demo deploy clean

sim:
	dotnet run --project src/VirtualVxg.Simulator -- --config src/VirtualVxg.Simulator/configs/unit-good.json

test:
	dotnet test

demo:
	bash scripts/demo.sh

deploy:
	cd deploy && fly deploy

clean:
	dotnet clean && rm -rf bin obj */bin */obj
