.PHONY: sim test demo plan-run deploy clean

sim:
	dotnet run --project src/VirtualVxg.Simulator -- --config src/VirtualVxg.Simulator/configs/unit-good.json

test:
	dotnet test

plan-run:
	export PATH="$$HOME/opentap:$$PATH" && \
		dotnet run --project src/VirtualVxg.Simulator -- --config src/VirtualVxg.Simulator/configs/unit-good.json --port 5025 & \
		SIM_PID=$$!; sleep 2; tap run plans/flatness-sweep.TapPlan; kill $$SIM_PID 2>/dev/null; true

demo:
	bash scripts/demo.sh

deploy:
	cd deploy && fly deploy

clean:
	dotnet clean && rm -rf bin obj */bin */obj
